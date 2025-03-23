いいね、それやってみよう！  
目的は：

> `C:\YourFolder` 内のファイルを、サイズ制限に応じてグループ分けして、**グループごとに新しいサブフォルダを作って移動する**。

---

## ✅ 完成イメージ

- 元のフォルダ: `C:\YourFolder`
- 出力フォルダ: `C:\GroupedOutput`
- 作られるフォルダ：
  ```
  C:\GroupedOutput\Group_1
  C:\GroupedOutput\Group_2
  ...
  ```

---

## ✅ コード全体例（`yield return` + グループごと移動）

```csharp
using System;
using System.Collections.Generic;
using System.IO;

class Program
{
    static void Main()
    {
        string sourceFolder = @"C:\YourFolder";
        string outputRoot = @"C:\GroupedOutput";
        long maxGroupSize = 1L * 1024 * 1024 * 1024; // 1GB

        var files = Directory.EnumerateFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly);
        int groupIndex = 1;

        foreach (var group in GroupFilesBySize_StreamYield(files, maxGroupSize))
        {
            string groupFolder = Path.Combine(outputRoot, $"Group_{groupIndex}");
            Directory.CreateDirectory(groupFolder);

            foreach (var file in group)
            {
                string fileName = Path.GetFileName(file);
                string destPath = Path.Combine(groupFolder, fileName);

                try
                {
                    File.Move(file, destPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to move {file} → {destPath}: {ex.Message}");
                }
            }

            Console.WriteLine($"Moved Group_{groupIndex}: {group.Count} files");
            groupIndex++;
        }

        Console.WriteLine("Done.");
    }

    static IEnumerable<List<string>> GroupFilesBySize_StreamYield(IEnumerable<string> files, long maxSize)
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
                continue; // スキップ
            }

            if (currentSize + size > maxSize && currentGroup.Count > 0)
            {
                yield return currentGroup;
                currentGroup = new List<string>();
                currentSize = 0;
            }

            currentGroup.Add(file);
            currentSize += size;
        }

        if (currentGroup.Count > 0)
        {
            yield return currentGroup;
        }
    }
}
```

---

## 📌 注意点

| 注意 | 内容 |
|------|------|
| `File.Move()` は上書き不可 | 同名ファイルがあったら例外になる（必要なら対策可） |
| サブディレクトリは対象外 | `SearchOption.TopDirectoryOnly`（深くしたければ変えてOK） |
| グループ内ファイル名がかぶる場合 | リネーム・サブサブフォルダ・エラースキップ対応が必要になる可能性 |

---

## 🔧 もし上書きしたくない or ファイル名かぶり対策したい？

こんな感じでファイル名に `_1`, `_2` みたいに追加して安全に移動できる👇

```csharp
string destPath = Path.Combine(groupFolder, fileName);
int suffix = 1;

while (File.Exists(destPath))
{
    destPath = Path.Combine(groupFolder, Path.GetFileNameWithoutExtension(fileName) + $"_{suffix}" + Path.GetExtension(fileName));
    suffix++;
}
```

---

## ✅ まとめ

- `yield return` で省メモリ
- グループごとにフォルダ作成してファイルを移動
- 拡張も簡単（ログ保存、圧縮、並列処理など）

---

試してみて、問題出たらすぐ直せるようにするよ！  
たとえば：「サブフォルダごと対応したい」とか、「移動じゃなくコピーにしたい」とか、あれば教えて！