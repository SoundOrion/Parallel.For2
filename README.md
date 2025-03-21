なるほど！これはかなり実用的なバッチ処理パターンですね！💡  
しかも「大量のファイル」「一時的な移動」「並列処理」…とやりがいあるタスク！

---

## 🎯 やりたいこと（要約）

1. **大量のファイルをサイズ単位で分割**し、  
2. **一時フォルダを作って移動**  
3. 各フォルダ単位で**並列に処理**  
4. 終わったら**元フォルダに戻す**  
5. 安定して高速（失敗リスク低く、UIやCLIでもOK）

---

## ✅ 設計方針（安定・高速・シンプル）

| 要素          | 設計提案 |
|---------------|---------|
| ファイル分割   | サイズ順にグループ化（例：100MB/グループ） |
| フォルダ管理   | 一時フォルダを動的に作成（GUIDやタイムスタンプで衝突回避） |
| 並列処理       | `Parallel.ForEach` で各フォルダ処理 |
| 安全な戻し処理 | 例外処理＋ログでエラーファイル追跡可能に |
| 安定性         | ファイルロックチェック、処理失敗時に再試行 or スキップ可 |

---

## 🧪 サンプルコード構成（C# コンソール／WPF 両対応）

ここでは **100MBずつファイルをグループ化 → 一時フォルダに → 並列処理 → 元に戻す** という流れを例にします。

### 🔧 設定パラメータ

```csharp
string sourceFolder = @"C:\Source";
string tempRoot = @"C:\TempWork";
long maxGroupSize = 100 * 1024 * 1024; // 100MB
```

---

### 🛠️ 処理フロー

```csharp
// 1. ファイル取得 & グループ化
var files = Directory.GetFiles(sourceFolder);
var fileGroups = GroupFilesBySize(files, maxGroupSize);

// 2. フォルダごとに並列処理
Parallel.ForEach(fileGroups, group =>
{
    string tempFolder = Path.Combine(tempRoot, Guid.NewGuid().ToString());
    Directory.CreateDirectory(tempFolder);

    try
    {
        // 2-1. 一時フォルダに移動
        foreach (var file in group)
        {
            string dest = Path.Combine(tempFolder, Path.GetFileName(file));
            File.Move(file, dest);
        }

        // 2-2. 任意の処理（ここに本処理を入れる）
        ProcessFolder(tempFolder);

        // 2-3. 元に戻す
        foreach (var file in Directory.GetFiles(tempFolder))
        {
            string dest = Path.Combine(sourceFolder, Path.GetFileName(file));
            File.Move(file, dest);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"エラー: {ex.Message}");
        // ログに記録 or 該当フォルダ残すなど
    }
    finally
    {
        // クリーンアップ
        if (Directory.Exists(tempFolder))
        {
            Directory.Delete(tempFolder, true);
        }
    }
});
```

---

### 🔍 サイズごとにグループ化するヘルパー

```csharp
List<List<string>> GroupFilesBySize(string[] files, long maxGroupSize)
{
    var groups = new List<List<string>>();
    var currentGroup = new List<string>();
    long currentSize = 0;

    foreach (var file in files.OrderBy(f => new FileInfo(f).Length))
    {
        long size = new FileInfo(file).Length;

        if (currentSize + size > maxGroupSize && currentGroup.Count > 0)
        {
            groups.Add(currentGroup);
            currentGroup = new List<string>();
            currentSize = 0;
        }

        currentGroup.Add(file);
        currentSize += size;
    }

    if (currentGroup.Count > 0)
        groups.Add(currentGroup);

    return groups;
}
```

---

### 🧪 任意の処理関数（モック）

```csharp
void ProcessFolder(string folderPath)
{
    // 例: 全ファイルを一括圧縮・画像変換・DB登録 など
    Thread.Sleep(500); // 仮処理
}
```

---

## ✅ 高速性・安定性を上げる工夫

| ポイント              | 方法 |
|-----------------------|------|
| ❗ ファイルロック対策     | `File.Open(..., FileShare.None)` で使われてないかチェック |
| 🧠 メモリ使用を抑制      | 一度に全ファイルを処理せずグループ単位で管理 |
| 🔥 並列度調整            | `ParallelOptions.MaxDegreeOfParallelism` |
| 📝 処理ログ・失敗記録     | ログファイル or コンソール出力 |
| 🔄 処理再実行可能に       | 失敗グループだけリトライできる構造にしておく |

---

## 🧩 WPF連携もできる

上記ロジックを `Task.Run` で包めば、WPFアプリでも UIフリーズなしに動かせます。  
`ProgressBar` も `fileGroups.Count` に応じて進捗を出せばOK！

---

## ✅ まとめ

- 🔧 安定性：ファイル分割・エラー処理・リトライ可能設計
- ⚡ 高速性：並列フォルダ処理 + サイズ単位バッチ
- 🧘‍♂️ 柔軟性：処理内容・分割単位・ログ対応も自由自在

---

👀 実際に使いたい処理内容（例：画像変換とか、ZIP圧縮とか）が決まっていれば、それにあわせてテンプレ出すよ！  
もしくはこのロジックをWPF UIに組み込みたい？どっち行く？


了解！じゃあ、**以下の要件に合った全体ソースコード**をWPFアプリ用でまとめます👇

---

## ✅ 要件まとめ

- 元フォルダに大量のファイルがある
- サイズごとに一時フォルダを作ってファイルを移動
- 一時フォルダごとに並列処理
- 処理後、元フォルダにファイルを戻す
- UIに `ProgressBar` + `開始` & `キャンセル` ボタン付き
- 安定性・高速性考慮済み

---

## 🧩 プロジェクト構成（WPF）

- `MainWindow.xaml`：UI（ボタン、プログレスバー）
- `MainWindow.xaml.cs`：メインロジック

---

### 📄 MainWindow.xaml

```xml
<Window x:Class="FileBatchProcessor.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        Title="ファイルバッチ処理" Height="200" Width="400">
    <StackPanel Margin="10">
        <ProgressBar x:Name="ProgressBar" Height="25" Minimum="0" Margin="0,0,0,10"/>
        <Button x:Name="StartButton" Content="開始" Click="StartButton_Click" Margin="0,0,0,5"/>
        <Button x:Name="CancelButton" Content="キャンセル" Click="CancelButton_Click" IsEnabled="False"/>
    </StackPanel>
</Window>
```

---

### 📄 MainWindow.xaml.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FileBatchProcessor
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _cts;
        private int _progress = 0;

        private readonly string sourceFolder = @"C:\Source";
        private readonly string tempRoot = @"C:\TempWork";
        private readonly long maxGroupSize = 100 * 1024 * 1024; // 100MB

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(sourceFolder))
            {
                MessageBox.Show("元フォルダが存在しません。");
                return;
            }

            StartButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            _progress = 0;
            ProgressBar.Value = 0;

            _cts = new CancellationTokenSource();

            var files = Directory.GetFiles(sourceFolder);
            var groups = GroupFilesBySize(files, maxGroupSize);

            ProgressBar.Maximum = groups.Count;

            try
            {
                await Task.Run(() =>
                {
                    Parallel.ForEach(groups, new ParallelOptions
                    {
                        CancellationToken = _cts.Token
                    }, group =>
                    {
                        _cts.Token.ThrowIfCancellationRequested();

                        string tempFolder = Path.Combine(tempRoot, Guid.NewGuid().ToString());
                        Directory.CreateDirectory(tempFolder);

                        try
                        {
                            // ファイルを移動
                            foreach (var file in group)
                            {
                                string dest = Path.Combine(tempFolder, Path.GetFileName(file));
                                File.Move(file, dest);
                            }

                            // 処理
                            ProcessFolder(tempFolder, _cts.Token);

                            // 元に戻す
                            foreach (var file in Directory.GetFiles(tempFolder))
                            {
                                string dest = Path.Combine(sourceFolder, Path.GetFileName(file));
                                File.Move(file, dest);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"処理エラー: {ex.Message}");
                        }
                        finally
                        {
                            // 一時フォルダ削除
                            if (Directory.Exists(tempFolder))
                            {
                                Directory.Delete(tempFolder, true);
                            }

                            int current = Interlocked.Increment(ref _progress);
                            Dispatcher.Invoke(() => ProgressBar.Value = current);
                        }
                    });
                });

                MessageBox.Show("すべての処理が完了しました！");
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("キャンセルされました。");
            }
            finally
            {
                StartButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }

        private List<List<string>> GroupFilesBySize(string[] files, long maxSize)
        {
            var result = new List<List<string>>();
            var currentGroup = new List<string>();
            long currentSize = 0;

            foreach (var file in files.OrderBy(f => new FileInfo(f).Length))
            {
                long size = new FileInfo(file).Length;

                if (currentSize + size > maxSize && currentGroup.Count > 0)
                {
                    result.Add(currentGroup);
                    currentGroup = new List<string>();
                    currentSize = 0;
                }

                currentGroup.Add(file);
                currentSize += size;
            }

            if (currentGroup.Count > 0)
                result.Add(currentGroup);

            return result;
        }

        private void ProcessFolder(string folderPath, CancellationToken token)
        {
            // 任意の処理（ここを好きに変えてOK）
            foreach (var file in Directory.GetFiles(folderPath))
            {
                token.ThrowIfCancellationRequested();
                // 模擬的な重い処理（画像変換、圧縮など）
                Thread.Sleep(100);
            }
        }
    }
}
```

---

## ✅ このサンプルの強み

- 🧠 サイズごとのグループ化 → メモリ効率◎
- ⚡ 並列処理で高速化（フォルダ単位）
- 🛑 キャンセル対応 (`CancellationToken`)
- 🔐 安定性重視（例外キャッチ・フォルダ後片付け）

---

## 🛠️ カスタマイズ例

| やりたいこと        | 変更点 |
|---------------------|--------|
| 処理内容を変えたい   | `ProcessFolder` を変更 |
| ファイルサイズではなく件数で分けたい | `GroupFilesBySize` を書き換え |
| ログを残したい       | `try-catch` の中でログファイルに書き出す |
| 元ファイルを残したい | `File.Copy` に変更する |

---

必要なら、ログ付き版・MVVM版・画像/動画処理特化版なども展開できるよ。  
やりたい処理の具体例があれば、そこにあわせて最適化しようか？🔥


いいアプローチ！✨  
まずは **コンソールアプリでロジックを掴む → WPFでUIと連携** って流れは超おすすめです！

---

## ✅ コンソール版：全体処理のベースロジック

やることはWPFと同じ👇

1. 元フォルダ内のファイルをサイズごとにグループ化  
2. 各グループを一時フォルダに移動  
3. 並列で処理（ダミー処理）  
4. 処理が終わったら元フォルダに戻す  
5. 途中でキャンセルできるように `CancellationToken` も入れる

---

### 🧩 コンソールアプリ用 完全コード

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static readonly string sourceFolder = @"C:\Source";
    static readonly string tempRoot = @"C:\TempWork";
    static readonly long maxGroupSize = 100 * 1024 * 1024; // 100MB

    static async Task Main(string[] args)
    {
        if (!Directory.Exists(sourceFolder))
        {
            Console.WriteLine("元フォルダが存在しません。");
            return;
        }

        Console.WriteLine("処理を開始します。キャンセルするには Ctrl+C を押してください。");

        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (s, e) =>
        {
            Console.WriteLine("キャンセル要求されました...");
            cts.Cancel();
            e.Cancel = true; // プロセスの終了は防ぐ
        };

        try
        {
            var files = Directory.GetFiles(sourceFolder);
            var groups = GroupFilesBySize(files, maxGroupSize);

            int total = groups.Count;
            int progress = 0;

            await Task.Run(() =>
            {
                Parallel.ForEach(groups, new ParallelOptions
                {
                    CancellationToken = cts.Token
                }, group =>
                {
                    cts.Token.ThrowIfCancellationRequested();

                    string tempFolder = Path.Combine(tempRoot, Guid.NewGuid().ToString());
                    Directory.CreateDirectory(tempFolder);

                    try
                    {
                        // 一時フォルダにファイル移動
                        foreach (var file in group)
                        {
                            string dest = Path.Combine(tempFolder, Path.GetFileName(file));
                            File.Move(file, dest);
                        }

                        // 処理本体
                        ProcessFolder(tempFolder, cts.Token);

                        // 元に戻す
                        foreach (var file in Directory.GetFiles(tempFolder))
                        {
                            string dest = Path.Combine(sourceFolder, Path.GetFileName(file));
                            File.Move(file, dest);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"エラー: {ex.Message}");
                    }
                    finally
                    {
                        // 後片付け
                        if (Directory.Exists(tempFolder))
                            Directory.Delete(tempFolder, true);

                        int done = Interlocked.Increment(ref progress);
                        Console.WriteLine($"進捗: {done}/{total} グループ完了");
                    }
                });
            });

            Console.WriteLine("すべての処理が完了しました！");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("キャンセルされました。");
        }
    }

    static List<List<string>> GroupFilesBySize(string[] files, long maxSize)
    {
        var result = new List<List<string>>();
        var currentGroup = new List<string>();
        long currentSize = 0;

        foreach (var file in files.OrderBy(f => new FileInfo(f).Length))
        {
            long size = new FileInfo(file).Length;

            if (currentSize + size > maxSize && currentGroup.Count > 0)
            {
                result.Add(currentGroup);
                currentGroup = new List<string>();
                currentSize = 0;
            }

            currentGroup.Add(file);
            currentSize += size;
        }

        if (currentGroup.Count > 0)
            result.Add(currentGroup);

        return result;
    }

    static void ProcessFolder(string folderPath, CancellationToken token)
    {
        // 模擬的な重い処理
        foreach (var file in Directory.GetFiles(folderPath))
        {
            token.ThrowIfCancellationRequested();
            Console.WriteLine($"  処理中: {Path.GetFileName(file)}");
            Thread.Sleep(100); // 重い処理の代わり
        }
    }
}
```

---

## ✅ 特徴

| 項目 | 内容 |
|------|------|
| ⏱ 並列処理 | `Parallel.ForEach` でフォルダ単位に高速実行 |
| 📦 一時フォルダ | `Guid.NewGuid()` で衝突回避 |
| 🛑 キャンセル対応 | `Ctrl+C`で中断できる (`Console.CancelKeyPress`) |
| 📊 進捗表示 | コンソールに現在の進捗を出力 |
| ♻️ 再利用しやすい | WPF移行時もロジックそのまま流用可 |

---

## ✅ WPF化のときのポイント

| ロジック部分        | UI版へ流用方法         |
|---------------------|-------------------------|
| `GroupFilesBySize`  | そのまま使える          |
| `ProcessFolder`     | 処理内容によって調整    |
| `Parallel.ForEach`  | `Task.Run()`で包んでUIと分離 |
| `Console.WriteLine` | `ProgressBar.Value` や `Label` に置き換え |

---

## ✨ ここまで来たら

- ロジック完全理解できたら WPFへ移行 ✅
- `ProcessFolder` の中身を本番処理に ✅
- ログ機能つけたい、エラーリカバリしたい、など拡張も超しやすい

---

他にやりたいこと、例えば「サイズじゃなくて件数でグループ化したい」とか「ZIP圧縮したい」とかあったら、それもすぐ加えられるよ！どう？試してみる？



OK！つまり、  
一時フォルダを `"C:\TempWork\GUID"` みたいに作るんじゃなくて、  
**固定で `"C:\Source_01"`, `"C:\Source_02"`…と番号付きで作成＆使い回す** ってことね 👍

---

## ✅ 更新ポイント

| 項目 | 変更内容 |
|------|----------|
| フォルダ名       | `"C:\Source_01"`, `"C:\Source_02"` などに変更（インデックス付き）  
| 再利用のため削除 | 実行前に前回の `"C:\Source_XX"` を削除してクリーン状態にする  
| グループごとに割り当て | `GroupFilesBySize` で分けたグループに1:1で対応するように作成

---

## 🔁 更新されたコードの該当部分（コンソール版）

以下、差分だけ紹介 → フルコード必要ならあとで統合します！

---

### 🔧 フォルダ名を `"C:\Source_01"` 形式で作成

```csharp
// 実行前：旧フォルダをクリーンアップして、新しく作る
void PrepareTempFolders(int groupCount)
{
    for (int i = 1; i <= groupCount; i++)
    {
        string path = $@"C:\Source_{i:D2}";

        if (Directory.Exists(path))
            Directory.Delete(path, true);

        Directory.CreateDirectory(path);
    }
}
```

---

### 🧩 実行時のメイン処理の変更点

```csharp
PrepareTempFolders(groups.Count);

await Task.Run(() =>
{
    Parallel.ForEach(groups.Select((group, index) => (group, index)), new ParallelOptions
    {
        CancellationToken = cts.Token
    }, item =>
    {
        var group = item.group;
        int index = item.index;

        string tempFolder = $@"C:\Source_{index + 1:D2}";
        Directory.CreateDirectory(tempFolder); // 念のため

        try
        {
            foreach (var file in group)
            {
                string dest = Path.Combine(tempFolder, Path.GetFileName(file));
                File.Move(file, dest);
            }

            ProcessFolder(tempFolder, cts.Token);

            foreach (var file in Directory.GetFiles(tempFolder))
            {
                string dest = Path.Combine(sourceFolder, Path.GetFileName(file));
                File.Move(file, dest);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"エラー: {ex.Message}");
        }
        finally
        {
            int done = Interlocked.Increment(ref progress);
            Console.WriteLine($"進捗: {done}/{groups.Count}");
        }
    });
});
```

---

### 👀 補足ポイント

- `Source_01`, `Source_02` のように **明示的にわかりやすいフォルダ名**を作れる
- `PrepareTempFolders()` によって **毎回クリーンに再生成**されるので安全
- グループ数に応じてフォルダが1:1で生成される

---

## ✅ フォルダ構成イメージ

```
C:\Source             ← 元のファイルが最初に入っている
C:\Source_01          ← グループ1（処理中に使う）
C:\Source_02          ← グループ2
C:\Source_03          ← グループ3
...
```

---

## ✅ 最終的にどうなるか

- 元ファイル群をサイズごとに分割
- 各グループを `Source_01`, `Source_02`... に移動
- 並列に処理
- 処理後にファイルを元に戻す
- 一時フォルダ（`Source_XX`）は空になる（or削除してもOK）

---

## 👉 次どうする？

- この差分を含めた**フルのコンソールコード**出す？
- もしくはこのまま**WPFに移植**する？
- `ProcessFolder` を何か特定の処理にしたい？

どこからでもOK！言ってくれたらすぐ対応するよ💪



よっしゃ！じゃあまずは…

---

# ✅① コンソールアプリ版（差分含むフルコード）

---

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static readonly string sourceFolder = @"C:\Source";
    static readonly string tempFolderFormat = @"C:\Source_{0:D2}";
    static readonly long maxGroupSize = 100 * 1024 * 1024; // 100MB

    static async Task Main(string[] args)
    {
        if (!Directory.Exists(sourceFolder))
        {
            Console.WriteLine("元フォルダが存在しません。");
            return;
        }

        Console.WriteLine("処理を開始します。キャンセルするには Ctrl+C を押してください。");

        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (s, e) =>
        {
            Console.WriteLine("キャンセル要求されました...");
            cts.Cancel();
            e.Cancel = true;
        };

        try
        {
            var files = Directory.GetFiles(sourceFolder);
            var groups = GroupFilesBySize(files, maxGroupSize);

            PrepareTempFolders(groups.Count);

            int total = groups.Count;
            int progress = 0;

            await Task.Run(() =>
            {
                Parallel.ForEach(groups.Select((group, index) => (group, index)), new ParallelOptions
                {
                    CancellationToken = cts.Token
                }, item =>
                {
                    var group = item.group;
                    int index = item.index;

                    string tempFolder = string.Format(tempFolderFormat, index + 1);
                    Directory.CreateDirectory(tempFolder); // 念のため

                    try
                    {
                        // 移動
                        foreach (var file in group)
                        {
                            string dest = Path.Combine(tempFolder, Path.GetFileName(file));
                            File.Move(file, dest);
                        }

                        // 任意処理
                        ProcessFolder(tempFolder, cts.Token);

                        // 元に戻す
                        foreach (var file in Directory.GetFiles(tempFolder))
                        {
                            string dest = Path.Combine(sourceFolder, Path.GetFileName(file));
                            File.Move(file, dest);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"エラー: {ex.Message}");
                    }
                    finally
                    {
                        int done = Interlocked.Increment(ref progress);
                        Console.WriteLine($"進捗: {done}/{total}");
                    }
                });
            });

            Console.WriteLine("すべての処理が完了しました！");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("キャンセルされました。");
        }
    }

    static List<List<string>> GroupFilesBySize(string[] files, long maxSize)
    {
        var result = new List<List<string>>();
        var currentGroup = new List<string>();
        long currentSize = 0;

        foreach (var file in files.OrderBy(f => new FileInfo(f).Length))
        {
            long size = new FileInfo(file).Length;

            if (currentSize + size > maxSize && currentGroup.Count > 0)
            {
                result.Add(currentGroup);
                currentGroup = new List<string>();
                currentSize = 0;
            }

            currentGroup.Add(file);
            currentSize += size;
        }

        if (currentGroup.Count > 0)
            result.Add(currentGroup);

        return result;
    }

    static void PrepareTempFolders(int groupCount)
    {
        for (int i = 1; i <= groupCount; i++)
        {
            string path = string.Format(tempFolderFormat, i);
            if (Directory.Exists(path))
                Directory.Delete(path, true);

            Directory.CreateDirectory(path);
        }
    }

    static void ProcessFolder(string folderPath, CancellationToken token)
    {
        foreach (var file in Directory.GetFiles(folderPath))
        {
            token.ThrowIfCancellationRequested();
            Console.WriteLine($"  処理中: {Path.GetFileName(file)}");
            Thread.Sleep(100); // 重い処理の代用
        }
    }
}
```

---

# ✅② WPFアプリ版（上記と同じ処理をUI付きで）

---

## 📄 MainWindow.xaml

```xml
<Window x:Class="WpfBatchFileProcessor.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        Title="WPFファイルバッチ処理" Height="200" Width="400">
    <StackPanel Margin="10">
        <ProgressBar x:Name="ProgressBar" Height="25" Minimum="0" Margin="0,0,0,10"/>
        <Button x:Name="StartButton" Content="開始" Click="StartButton_Click" Margin="0,0,0,5"/>
        <Button x:Name="CancelButton" Content="キャンセル" Click="CancelButton_Click" IsEnabled="False"/>
    </StackPanel>
</Window>
```

---

## 📄 MainWindow.xaml.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace WpfBatchFileProcessor
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _cts;
        private int _progress = 0;

        private readonly string sourceFolder = @"C:\Source";
        private readonly string tempFolderFormat = @"C:\Source_{0:D2}";
        private readonly long maxGroupSize = 100 * 1024 * 1024;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(sourceFolder))
            {
                MessageBox.Show("元フォルダが存在しません。");
                return;
            }

            StartButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            _progress = 0;
            ProgressBar.Value = 0;

            _cts = new CancellationTokenSource();

            var files = Directory.GetFiles(sourceFolder);
            var groups = GroupFilesBySize(files, maxGroupSize);
            ProgressBar.Maximum = groups.Count;

            PrepareTempFolders(groups.Count);

            try
            {
                await Task.Run(() =>
                {
                    Parallel.ForEach(groups.Select((group, index) => (group, index)), new ParallelOptions
                    {
                        CancellationToken = _cts.Token
                    }, item =>
                    {
                        var group = item.group;
                        int index = item.index;

                        string tempFolder = string.Format(tempFolderFormat, index + 1);
                        Directory.CreateDirectory(tempFolder);

                        try
                        {
                            foreach (var file in group)
                            {
                                string dest = Path.Combine(tempFolder, Path.GetFileName(file));
                                File.Move(file, dest);
                            }

                            ProcessFolder(tempFolder, _cts.Token);

                            foreach (var file in Directory.GetFiles(tempFolder))
                            {
                                string dest = Path.Combine(sourceFolder, Path.GetFileName(file));
                                File.Move(file, dest);
                            }
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() => MessageBox.Show($"エラー: {ex.Message}"));
                        }
                        finally
                        {
                            int done = Interlocked.Increment(ref _progress);
                            Dispatcher.Invoke(() => ProgressBar.Value = done);
                        }
                    });
                });

                MessageBox.Show("完了しました！");
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("キャンセルされました。");
            }
            finally
            {
                StartButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }

        private List<List<string>> GroupFilesBySize(string[] files, long maxSize)
        {
            var result = new List<List<string>>();
            var currentGroup = new List<string>();
            long currentSize = 0;

            foreach (var file in files.OrderBy(f => new FileInfo(f).Length))
            {
                long size = new FileInfo(file).Length;

                if (currentSize + size > maxSize && currentGroup.Count > 0)
                {
                    result.Add(currentGroup);
                    currentGroup = new List<string>();
                    currentSize = 0;
                }

                currentGroup.Add(file);
                currentSize += size;
            }

            if (currentGroup.Count > 0)
                result.Add(currentGroup);

            return result;
        }

        private void PrepareTempFolders(int groupCount)
        {
            for (int i = 1; i <= groupCount; i++)
            {
                string path = string.Format(tempFolderFormat, i);
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
                Directory.CreateDirectory(path);
            }
        }

        private void ProcessFolder(string folderPath, CancellationToken token)
        {
            foreach (var file in Directory.GetFiles(folderPath))
            {
                token.ThrowIfCancellationRequested();
                Thread.Sleep(100); // 仮の重い処理
            }
        }
    }
}
```

---

## 🧠 完全理解へ！

- コンソールで流れ＆仕組みを把握 ✅  
- WPFで非同期・UI連携を体感 ✅  
- 処理本体（`ProcessFolder`）を差し替えて本番対応へ ✅

---

さらに「エラーログ保存したい」「処理結果を一覧で見たい」などもどんどん足せる！  
やってみて詰まったとこあったら、いつでも聞いて！🔥