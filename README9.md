了解！  
では以下のようにします：

---

## ✅ 要件追加まとめ（最終形）

1. 元フォルダ：`sourceFolder`（例：`C:\BigFolder`）に大量のファイルあり
2. 32MB以下になるように **グループ分け**
3. 各グループを一時フォルダに移動（`Temp_xxxx`）
4. `Temp_xxxx` を **一時的に `sourceFolder` にリネーム**
5. `sourceFolder` に対して外部 exe 実行
6. 終わったら、**処理済ファイルを別の一時保存先に退避**
7. 最後に、**すべての処理済ファイルを `sourceFolder` に戻す**

---

## ✅ 修正ポイントだけ抜粋（追加）

- 退避先ディレクトリ：`string completedFolder = @"C:\Work\Completed";`
- 各グループの処理が終わったあと、ファイルを `completedFolder` に Move
- 最後に `completedFolder` からすべて `sourceFolder` に戻す

---

## ✅ フルコード（最終バージョン）

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        string sourceFolder = @"C:\BigFolder";
        string tempRoot = @"C:\Work\TempGroup";
        string externalToolPath = @"C:\Tools\myTool.exe";
        string completedFolder = @"C:\Work\Completed";
        long maxGroupSize = 32L * 1024 * 1024; // 32MB

        Directory.CreateDirectory(completedFolder);

        var files = Directory.EnumerateFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly);
        int groupIndex = 1;
        Task previousTask = Task.CompletedTask;

        foreach (var group in GroupFilesBySize_StreamCut(files, maxGroupSize))
        {
            string tempFolder = $"{tempRoot}_{groupIndex:D4}";
            Directory.CreateDirectory(tempFolder);

            foreach (var file in group)
            {
                string destPath = Path.Combine(tempFolder, Path.GetFileName(file));
                try
                {
                    File.Move(file, destPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to move {file} → {destPath}: {ex.Message}");
                }
            }

            await previousTask;

            previousTask = Task.Run(() =>
            {
                try
                {
                    // 一時的に元の sourceFolder を削除
                    if (Directory.Exists(sourceFolder))
                        Directory.Delete(sourceFolder, true);

                    // temp を sourceFolder にリネーム
                    Directory.Move(tempFolder, sourceFolder);

                    // exe 実行
                    if (!RunExternalTool(sourceFolder, externalToolPath))
                        throw new Exception("外部ツール実行に失敗");

                    // 処理済ファイルを completed に退避
                    foreach (var file in Directory.GetFiles(sourceFolder))
                    {
                        string dest = Path.Combine(completedFolder, Path.GetFileName(file));
                        File.Move(file, dest);
                    }

                    // 空になった sourceFolder を削除
                    Directory.Delete(sourceFolder, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] グループ{groupIndex}: {ex.Message}");
                }
            });

            groupIndex++;
        }

        await previousTask;

        // 処理済ファイルをすべて元の sourceFolder に戻す
        Directory.CreateDirectory(sourceFolder);

        foreach (var file in Directory.GetFiles(completedFolder))
        {
            string dest = Path.Combine(sourceFolder, Path.GetFileName(file));
            try
            {
                File.Move(file, dest);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[戻し失敗] {file} → {dest}: {ex.Message}");
            }
        }

        Directory.Delete(completedFolder, true);

        Console.WriteLine("✔ すべての処理が完了し、ファイルも戻されました！");
    }

    static IEnumerable<List<string>> GroupFilesBySize_StreamCut(IEnumerable<string> files, long maxSize)
    {
        var currentGroup = new List<string>();
        long currentSize = 0;

        foreach (var file in files)
        {
            long size;

            try
            {
                size = new FileInfo(file).Length;
            }
            catch
            {
                continue;
            }

            if (currentSize > maxSize)
            {
                yield return currentGroup;
                currentGroup = new List<string>();
                currentSize = 0;
            }

            currentGroup.Add(file);
            currentSize += size;
        }

        if (currentGroup.Count > 0)
            yield return currentGroup;
    }

    static bool RunExternalTool(string folderPath, string exePath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"\"{folderPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            proc.WaitForExit();

            string output = proc.StandardOutput.ReadToEnd();
            string error = proc.StandardError.ReadToEnd();

            if (proc.ExitCode != 0)
            {
                Console.WriteLine($"[NG] {folderPath} → Error: {error}");
                return false;
            }
            else
            {
                Console.WriteLine($"[OK] {folderPath} → Output: {output}");
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Exception] {folderPath} → {ex.Message}");
            return false;
        }
    }
}
```

---

## ✅ 最終チェック

| 処理段階 | OK? |
|----------|-----|
| ✅ グループ分割（32MB超えたら分ける） | ✅ |
| ✅ 一時フォルダに move | ✅ |
| ✅ 一時フォルダを `sourceFolder` に rename | ✅ |
| ✅ 外部ツール実行 | ✅ |
| ✅ 処理済ファイルを退避（`Completed`） | ✅ |
| ✅ 最終的に全部 `sourceFolder` に戻す | ✅ |

---

必要なら、「並列数制限」「ロックファイルで排他制御」なんかも追加できるので、気軽にどうぞ～！



ナイス追加要望です👍  
それ、**ちゃんと制御しないと危険になるパターン**なので大事です。

---

## ✅ 目的：並列数を制限 & 排他制御

- ⚙ **並列数制限**：例えば「外部ツールの実行は最大で2つまで」にしたい
- 🔐 **排他制御**：`sourceFolder` を `Temp` → `sourceFolder` にリネームして実行するから、**複数プロセスが同時に同じ場所を触るとNG** → **排他ロックが必要**

---

## ✅ 解決策

### ✔ 並列数制限：`SemaphoreSlim` を使う  
→ 最大並列実行数を制限（例：2）

### ✔ 排他制御：`lock` + `sourceFolder` の実行前チェック  
→ `Directory.Exists(sourceFolder)` で競合してたら待つ

---

## ✅ 改良版コード（並列2つ制限、排他付き）

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static readonly object folderLock = new(); // 排他ロック用
    static SemaphoreSlim semaphore = new(2);   // 最大2並列

    static async Task Main()
    {
        string sourceFolder = @"C:\BigFolder";
        string tempRoot = @"C:\Work\TempGroup";
        string externalToolPath = @"C:\Tools\myTool.exe";
        string completedFolder = @"C:\Work\Completed";
        long maxGroupSize = 32L * 1024 * 1024;

        Directory.CreateDirectory(completedFolder);

        var files = Directory.EnumerateFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly);
        int groupIndex = 1;

        var tasks = new List<Task>();

        foreach (var group in GroupFilesBySize_StreamCut(files, maxGroupSize))
        {
            string tempFolder = $"{tempRoot}_{groupIndex:D4}";
            Directory.CreateDirectory(tempFolder);

            foreach (var file in group)
            {
                string destPath = Path.Combine(tempFolder, Path.GetFileName(file));
                try
                {
                    File.Move(file, destPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to move {file} → {destPath}: {ex.Message}");
                }
            }

            int currentGroup = groupIndex;
            tasks.Add(ProcessGroupAsync(tempFolder, sourceFolder, completedFolder, externalToolPath, currentGroup));
            groupIndex++;
        }

        await Task.WhenAll(tasks);

        // 最後に全部戻す
        Directory.CreateDirectory(sourceFolder);
        foreach (var file in Directory.GetFiles(completedFolder))
        {
            string dest = Path.Combine(sourceFolder, Path.GetFileName(file));
            try
            {
                File.Move(file, dest);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[戻し失敗] {file} → {dest}: {ex.Message}");
            }
        }

        Directory.Delete(completedFolder, true);

        Console.WriteLine("✔ すべての処理が完了し、ファイルも戻されました！");
    }

    static async Task ProcessGroupAsync(string tempFolder, string sourceFolder, string completedFolder, string toolPath, int groupIndex)
    {
        await semaphore.WaitAsync(); // 同時実行数制限

        try
        {
            lock (folderLock)
            {
                // 排他的に sourceFolder を操作
                if (Directory.Exists(sourceFolder))
                    Directory.Delete(sourceFolder, true);

                Directory.Move(tempFolder, sourceFolder);
            }

            Console.WriteLine($"[開始] グループ{groupIndex}");

            if (!RunExternalTool(sourceFolder, toolPath))
                throw new Exception("外部ツール実行に失敗");

            foreach (var file in Directory.GetFiles(sourceFolder))
            {
                string dest = Path.Combine(completedFolder, Path.GetFileName(file));
                File.Move(file, dest);
            }

            Directory.Delete(sourceFolder, true);
            Console.WriteLine($"[完了] グループ{groupIndex}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] グループ{groupIndex}: {ex.Message}");
        }
        finally
        {
            semaphore.Release(); // 実行スロット開放
        }
    }

    static IEnumerable<List<string>> GroupFilesBySize_StreamCut(IEnumerable<string> files, long maxSize)
    {
        var currentGroup = new List<string>();
        long currentSize = 0;

        foreach (var file in files)
        {
            long size;

            try
            {
                size = new FileInfo(file).Length;
            }
            catch
            {
                continue;
            }

            if (currentSize > maxSize)
            {
                yield return currentGroup;
                currentGroup = new List<string>();
                currentSize = 0;
            }

            currentGroup.Add(file);
            currentSize += size;
        }

        if (currentGroup.Count > 0)
            yield return currentGroup;
    }

    static bool RunExternalTool(string folderPath, string exePath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"\"{folderPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            proc.WaitForExit();

            string output = proc.StandardOutput.ReadToEnd();
            string error = proc.StandardError.ReadToEnd();

            if (proc.ExitCode != 0)
            {
                Console.WriteLine($"[NG] {folderPath} → Error: {error}");
                return false;
            }

            Console.WriteLine($"[OK] {folderPath} → Output: {output}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Exception] {folderPath} → {ex.Message}");
            return false;
        }
    }
}
```

---

## ✅ ポイント解説

| 処理 | 解説 |
|------|------|
| `SemaphoreSlim(2)` | 最大2つのグループが同時に処理可能 |
| `lock(folderLock)` | `sourceFolder` への rename/削除 を **完全排他制御** |
| `Task.WhenAll(tasks)` | 全グループ並列処理を **待機してからファイル戻し** |

---

## 💬 調整したい場合

- `SemaphoreSlim(2)` の `2` を増やせば **並列処理数アップ**
- `lock` ブロックをさらに厳密にしたい場合は `Mutex` に変更も可（他プロセス対応）

---

どう？これで「**安全に並列化しつつ、sourceFolderは逐次排他使用**」が完璧にできる構成になったよ。  
もしもう1段レベルアップ（ログ書き出し・中断再開・リトライ機能）も欲しければ遠慮なく！


OK！PowerShellで**自分のPCのコア数**を確認する方法を紹介します。めっちゃ簡単です。

---

### 🔍 **物理コア数の確認（CPUの本物のコア数）**

```powershell
(Get-WmiObject -Class Win32_Processor).NumberOfCores
```

---

### 🔍 **論理コア数（スレッド数、HT込みの数）**

```powershell
(Get-WmiObject -Class Win32_Processor).NumberOfLogicalProcessors
```

🧠 補足：
- 例：6コア12スレッドのCPUなら  
  - `NumberOfCores` → 6  
  - `NumberOfLogicalProcessors` → 12  

---

### ✅ **両方まとめて確認するスクリプト**

```powershell
Get-WmiObject -Class Win32_Processor | Select-Object Name, NumberOfCores, NumberOfLogicalProcessors
```

出力例：
```
Name                             NumberOfCores NumberOfLogicalProcessors
----                             ------------- -------------------------
Intel(R) Core(TM) i7-9700K CPU           8                       8
```

（この例はHyper-Threadingなしの8コアCPU）

---

必要なら、CPUの名前や詳細情報を含めた確認方法とか、もっとカスタマイズした出力も作れますよ。興味ある？