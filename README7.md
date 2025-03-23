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


めちゃくちゃ良い視点です！💯  
そう、**ファイルを移動しながら同じフォルダを列挙してる**ときに「おかしなことが起きるのでは？」って疑問、すごく正しい。

---

## ✅ 結論：**基本的には安全だけど、状況によって注意が必要**

### ✔️ 安全な理由（今回のコードの場合）

- ファイルの列挙はこの1行で**最初に全部取得**してる：

```csharp
var files = Directory.EnumerateFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly);
```

➡️ この `IEnumerable` は「遅延評価」だけど、`foreach` の前に `.ToArray()` や `.ToList()` にしておけば**列挙時に固定される**。  
つまり、**移動が列挙に影響しない**。

---

## 😬 でも、次のようなケースは注意！

### ❗ NG 例（安全じゃない）

```csharp
foreach (var file in Directory.EnumerateFiles(...))
{
    File.Move(file, ...); // ← 列挙中に対象フォルダの中を変えてる
}
```

この場合、**列挙中に移動するとイテレータが壊れる可能性**があります。

---

## ✅ 対策：ファイル一覧を事前に確定させる

```csharp
var files = Directory.EnumerateFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly).ToArray();
```

または

```csharp
var files = Directory.GetFiles(sourceFolder);
```

これで、**列挙結果がメモリ上に確定される**ので、  
後からファイルを動かしても安全 ✅

---

## 🧠 遅延評価版と安全性の話

| 方法 | 安全？ | 理由 |
|------|--------|------|
| `Directory.EnumerateFiles()`（そのまま使う） | ❌ 潜在的に危険 | フォルダが変わると列挙に影響あり |
| `.ToArray()` / `.ToList()` で固定化 | ✅ 安全 | 列挙対象を最初にメモリに確保するため |
| `GetFiles()`（全部先に取得） | ✅ 安全 | 同上（ただし遅延評価できない） |

---

## ✅ まとめ

- ✔️ 今の実装でも `.ToArray()` を先に挟んでいれば OK
- ⚠️ `Directory.EnumerateFiles(...).AsEnumerable()` をそのまま使ってるなら危険！
- 💡 安全第一なら **最初にファイル一覧を配列化してから使うのが鉄則**

---

## 🔧 安全バージョン（改良）

```csharp
var files = Directory.EnumerateFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly).ToArray();
```

これを使ってから `yield` すれば、安心して `File.Move()` していける！

---

もっと応用編として、サブディレクトリ付きの再帰移動とか、ロック中ファイルのスキップとかも実装できるよ。  
必要なら次いこっか？


了解です！  
つまり以下のようにしたいんですね：

---

### ✅ 要件まとめ：

- **ファイルサイズの昇順ソートなし**
- 順番通りに処理しながら
- **合計サイズが32MB（例）を超えたら次のグループへ**
- つまり「**32MBを超えたらグループを切る**」スタイル

---

### ✅ 対応コード（ソートなし・サイズ超えで分割）

```csharp
using System;
using System.Collections.Generic;
using System.IO;

class Program
{
    static void Main()
    {
        string sourceFolder = @"C:\Users\Hatsuyama\Desktop\test";
        string outputRoot = @"C:\Users\Hatsuyama\Desktop";
        long maxGroupSize = 32L * 1024 * 1024; // 32MB

        var files = Directory.EnumerateFiles(sourceFolder, "*", SearchOption.TopDirectoryOnly);
        int groupIndex = 1;

        foreach (var group in GroupFilesBySize_StreamCut(files, maxGroupSize))
        {
            string groupFolder = Path.Combine(outputRoot, $"Group_{groupIndex}");
            Directory.CreateDirectory(groupFolder);

            foreach (var file in group)
            {
                string destPath = Path.Combine(groupFolder, Path.GetFileName(file));
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
                continue; // ファイル情報取得できない場合はスキップ
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
}
```

---

### 🧠 この処理の特徴

- **順番を維持して処理**（ファイル列挙順に依存）
- **サイズが maxGroupSize を**「**超えたタイミング**」**で次のグループに切り替え**
- ファイル1つが 32MB より大きくてもそのまま1グループとして扱われる（これは要件次第で変えられます）

---

もし「**32MB を超える前に止めたい（超えないようにしたい）**」という動きが良ければ、条件を `currentSize + size > maxSize` にすればOKです。変更希望あれば言ってください！

どうします？超え「たら」か、超え「ないように」か。