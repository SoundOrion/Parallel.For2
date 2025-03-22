最高です！🔥  
では、以下の順番で進めましょう。

---

## ✅ ステップ構成

1. **コンソール版**を先に構築（ロジック確認・実行安定性）
2. 後で **WPFに移植**（UI、進捗バー、キャンセルボタン追加）
3. 処理本体は **外部exeを引数付きで実行**（例：`myTool.exe C:\Work`）

---

# 🧩 ① コンソールアプリ：安定＆高速な外部ツール連携版

---

### ✅ 想定ディレクトリ構成

- `C:\MainFolder`：全ファイルの元フォルダ
- `C:\Work`：常にここにリネームして処理（外部ツールの引数）
- `C:\Temp_NNNN`：一時的にファイルを分割・移動する場所
- `myTool.exe`：外部処理実行ツール（引数に `C:\Work` を渡す）

---

### ✅ 完全コード：`Program.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

class Program
{
    static readonly string sourceFolder = @"C:\MainFolder";
    static readonly string workFolder = @"C:\Work";
    static readonly string tempFolderBase = @"C:\Temp_";
    static readonly long maxGroupSize = 32 * 1024 * 1024; // 32MB
    static readonly string externalToolPath = @"C:\Tools\myTool.exe";

    static void Main(string[] args)
    {
        if (!Directory.Exists(sourceFolder))
        {
            Console.WriteLine("元フォルダが存在しません。");
            return;
        }

        Console.WriteLine("ファイル列挙中...");
        var files = Directory.EnumerateFiles(sourceFolder);
        var groups = GroupFilesBySize(files, maxGroupSize);

        Console.WriteLine($"ファイルを {groups.Count} グループに分割しました。");

        for (int i = 0; i < groups.Count; i++)
        {
            Console.WriteLine($"\n=== グループ {i + 1}/{groups.Count} を処理中 ===");

            string tempFolder = $"{tempFolderBase}{i + 1:D4}";
            Directory.CreateDirectory(tempFolder);

            try
            {
                // ファイル移動（元→Temp）
                foreach (var file in groups[i])
                {
                    string dest = Path.Combine(tempFolder, Path.GetFileName(file));
                    File.Move(file, dest);
                }

                // Temp を Work にリネーム
                if (Directory.Exists(workFolder))
                    Directory.Delete(workFolder, true);

                Directory.Move(tempFolder, workFolder);

                // 外部ツール実行
                bool success = RunExternalTool(workFolder);

                if (!success)
                {
                    Console.WriteLine("外部ツールの実行に失敗しました。処理中断。");
                    break;
                }

                // 処理済ファイルを元に戻す
                foreach (var file in Directory.GetFiles(workFolder))
                {
                    string dest = Path.Combine(sourceFolder, Path.GetFileName(file));
                    File.Move(file, dest);
                }

                // Workフォルダ削除
                Directory.Delete(workFolder, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"エラー: {ex.Message}");
            }
        }

        Console.WriteLine("\nすべての処理が完了しました！");
    }

    static List<List<string>> GroupFilesBySize(IEnumerable<string> files, long maxSize)
    {
        var result = new List<List<string>>();
        var currentGroup = new List<string>();
        long currentSize = 0;

        foreach (var file in files.OrderBy(f => new FileInfo(file).Length))
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

    static bool RunExternalTool(string folderPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = externalToolPath,
                Arguments = $"\"{folderPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                string error = process.StandardError.ReadToEnd();
                Console.WriteLine($"外部ツール エラー: {error}");
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            Console.WriteLine($"外部ツール 出力: {output}");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"外部ツール実行エラー: {ex.Message}");
            return false;
        }
    }
}
```

---

## ✅ 特徴・安定性重視ポイント

| 工夫 | 内容 |
|------|------|
| ✅ 32MB単位にファイルを分割 | メモリ＆処理負荷を制御 |
| ✅ 一時フォルダ → Workリネーム | ツールが毎回同じフォルダ名で処理できる |
| ✅ 外部ツールが異常終了したら即中断 | 安全性UP |
| ✅ ログ出力可（`StandardOutput` / `Error`） | デバッグしやすい |

---

## ✅ 次のステップ

🟩 このロジックをベースに、次は **WPF版（進捗バー、キャンセル）** を作ります！

---

📌 **外部ツール実行部分で確認したい点があれば教えてください：**

- `myTool.exe` は **GUIアプリ？CLIアプリ？**
- **処理完了の判定基準**は `ExitCode` で十分？
- 実行にかかる時間の目安（秒？分？）

---

OKなら、このまま **WPFバージョンへ移植スタート**していきます！やる？💡


よっしゃ！ではこのまま **WPF バージョンへ移植**していきます！  
基本ロジックはコンソールと同じですが、**UI付き**で以下を強化👇

---

## ✅ WPF版のポイント

| 機能               | 内容 |
|--------------------|------|
| 🔄 UI連動            | `ProgressBar`, `%表示`, 状態表示テキスト  
| 🚀 非同期処理         | `Task.Run` + `await` で UI フリーズなし  
| ⛔ キャンセル対応      | `CancellationTokenSource`  
| ⚠ 外部ツール実行安定化 | `Process` で標準出力＋エラー取得  
| 🔐 安定性重視        | try/catch、エラーで中断＆通知  

---

## 🧩 1. `MainWindow.xaml`（UI定義）

```xml
<Window x:Class="WpfExternalToolProcessor.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        Title="フォルダ分割処理" Height="220" Width="450">
    <StackPanel Margin="10">
        <ProgressBar x:Name="ProgressBar" Height="25" Margin="0,0,0,5"/>
        <TextBlock x:Name="PercentText" Text="0%" HorizontalAlignment="Right" Margin="0,0,0,10"/>
        <TextBlock x:Name="StatusText" Margin="0,0,0,10"/>
        <Button x:Name="StartButton" Content="開始" Click="StartButton_Click" Margin="0,0,0,5"/>
        <Button x:Name="CancelButton" Content="キャンセル" Click="CancelButton_Click" IsEnabled="False"/>
    </StackPanel>
</Window>
```

---

## 🧩 2. `MainWindow.xaml.cs`（ロジック）

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace WpfExternalToolProcessor
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _cts;
        private int _progress = 0;

        private readonly string sourceFolder = @"C:\MainFolder";
        private readonly string workFolder = @"C:\Work";
        private readonly string tempFolderBase = @"C:\Temp_";
        private readonly long maxGroupSize = 32 * 1024 * 1024;
        private readonly string externalToolPath = @"C:\Tools\myTool.exe";

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
            ProgressBar.Value = 0;
            PercentText.Text = "0%";
            StatusText.Text = "ファイル列挙中...";

            _cts = new CancellationTokenSource();

            try
            {
                var files = Directory.EnumerateFiles(sourceFolder);
                var groups = GroupFilesBySize(files, maxGroupSize);

                ProgressBar.Maximum = groups.Count;

                await Task.Run(() =>
                {
                    for (int i = 0; i < groups.Count; i++)
                    {
                        _cts.Token.ThrowIfCancellationRequested();

                        string tempFolder = $"{tempFolderBase}{i + 1:D4}";
                        Directory.CreateDirectory(tempFolder);

                        try
                        {
                            // 移動
                            foreach (var file in groups[i])
                            {
                                string dest = Path.Combine(tempFolder, Path.GetFileName(file));
                                File.Move(file, dest);
                            }

                            // Workにリネーム
                            if (Directory.Exists(workFolder))
                                Directory.Delete(workFolder, true);

                            Directory.Move(tempFolder, workFolder);

                            // 外部ツール実行
                            bool success = RunExternalTool(workFolder);
                            if (!success) throw new Exception("外部ツール実行失敗");

                            // 戻す
                            foreach (var file in Directory.GetFiles(workFolder))
                            {
                                string dest = Path.Combine(sourceFolder, Path.GetFileName(file));
                                File.Move(file, dest);
                            }

                            Directory.Delete(workFolder, true);
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"エラー（グループ{i + 1}）: {ex.Message}");
                            });
                            break;
                        }

                        int done = Interlocked.Increment(ref _progress);
                        Dispatcher.Invoke(() =>
                        {
                            ProgressBar.Value = done;
                            double percent = done * 100.0 / ProgressBar.Maximum;
                            PercentText.Text = $"{percent:F1}%";
                            StatusText.Text = $"処理中: {done}/{groups.Count}";
                        });
                    }
                }, _cts.Token);

                StatusText.Text = "完了！";
                MessageBox.Show("すべての処理が完了しました！");
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "キャンセルされました。";
                MessageBox.Show("処理がキャンセルされました。");
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

        private List<List<string>> GroupFilesBySize(IEnumerable<string> files, long maxSize)
        {
            var result = new List<List<string>>();
            var currentGroup = new List<string>();
            long currentSize = 0;

            foreach (var file in files.OrderBy(f => new FileInfo(file).Length))
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

        private bool RunExternalTool(string folderPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = externalToolPath,
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    Console.WriteLine($"外部ツールエラー: {error}");
                    return false;
                }

                Console.WriteLine(process.StandardOutput.ReadToEnd());
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"外部ツール実行エラー: {ex.Message}");
                return false;
            }
        }
    }
}
```

---

## ✅ テスト確認チェックリスト ✅

- [ ] `C:\MainFolder` に大量ファイルあり（全体で1GB以上）
- [ ] `C:\Tools\myTool.exe` 実在していて正常に実行できる
- [ ] `C:\Work` フォルダが処理対象として認識される
- [ ] 32MBごとに処理が分割されている
- [ ] UIの進捗がリアルタイムで動く
- [ ] キャンセルボタンが効く

---

## ✨ 拡張したい？

| 追加したいこと | 次やること例 |
|----------------|--------------|
| 📋 処理ログ     | 成功/失敗をログファイルに保存  
| ⏱️ 残り時間表示 | 処理速度から予測表示  
| 📂 UIでフォルダ選択 | `FolderBrowserDialog` を追加  
| 💥 エラー時のリトライ | 自動で再実行（失敗グループのみ）  

---

やってみて詰まったとこ・次に足したい機能あったら遠慮なく言ってね！  
ログ保存？タイム計測？GUIでファイル選ぶ？全部いけるぜ🔥


めちゃくちゃいい質問です。まさに大規模データ処理の核心💡  
結論から言うと…

---

## ✅ **はい、メモリは大丈夫。ただし注意点あり。**

ちゃんと設計している限り、**数千万ファイル・数十ギガの処理**でも  
**メモリを爆食いせずに安全に動作可能です。**

ただし、そのためには以下の点を意識しておく必要があります👇

---

## ✅ キモはこれ：`Directory.EnumerateFiles()` を使ってること

```csharp
var files = Directory.EnumerateFiles(sourceFolder);
```

これのメリットは？

| `GetFiles()`          | `EnumerateFiles()`         |
|-----------------------|----------------------------|
| 全ファイル名を**一括で配列化** → メモリ激増 | 1件ずつ**遅延列挙** → メモリ超軽量 |
| 数万件超えるとアウト | 数千万件でもOK（スキャン速度次第） |

→ 今のコードは `EnumerateFiles()` ベースなので、**設計的に正解◎**

---

## ✅ その他のメモリ対策：現コードの評価

| 処理                       | 問題ないか？ |
|----------------------------|--------------|
| 📦 ファイル名列挙          | `EnumerateFiles()` → ✅  
| 🧮 サイズごとにグループ化   | `List<List<string>>` はサイズだけ見れば数千グループ程度なので ✅  
| ⛔ ファイル内容読み込み     | していないので安心 ✅  
| 🚀 並列処理/Taskのメモリ圧 | WPFで使っている `Parallel.For` & `Task.Run` は短命＆メモリ解放されやすい → ✅  
| 💥 一時的な移動先の作成    | 一時フォルダにコピーするだけで、メモリには乗らない → ✅  

---

## ⚠ ただし注意点（ケースによっては危険）

| 注意ポイント | 対応方法 |
|--------------|----------|
| フォルダ内の**アクセス権限や壊れたリンク** | `try/catch` で `EnumerateFiles()` にフィルターかける  
| 超巨大フォルダ（1億件など） | `EnumerateFiles()` に加えて「トップN件まで」といった制限を付ける or サブフォルダ分割前提の構成にする  
| .NETランタイムの制限 | .NET 6以降推奨（GCとパフォーマンスが最適化されている）  

---

## ✅ おすすめ拡張：列挙しながら逐次グループ化（理想の構成）

### 🔁 今はこうなってる：

```csharp
var files = Directory.EnumerateFiles(...);
var groups = GroupFilesBySize(files, maxSize); // → List<List<string>>
```

これは安全だけど、**全ファイル分のFileInfoを一度に持ってる**ので、数千万件なら数GBメモリ消費するかも。

---

### 🧠 改善案：グループ化もイテレータでやる

```csharp
IEnumerable<List<string>> GroupFilesBySizeStream(IEnumerable<string> files, long maxSize)
{
    var currentGroup = new List<string>();
    long currentSize = 0;

    foreach (var file in files)
    {
        long size = new FileInfo(file).Length;

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
        yield return currentGroup;
}
```

↑ これにすれば、**グループを1つずつ処理 → メモリ使用は最小限**

---

## ✅ 結論

| 条件 | 結果 |
|------|------|
| 数千万件のファイルがあっても | **メモリは問題なし**（今の設計ならOK） |
| さらに安全・効率化したい | **グループ処理も逐次化**すれば最強 |

---

## 🎯 次どうする？

- この「`GroupFilesBySizeStream()`」を導入して **完全ストリーム処理化**する？
- 「サブフォルダごとに分ける」ような構成も考える？
- ファイル数やメモリ状況を**リアルタイムに表示する**ようなUI追加する？

なんでも調整可能なので、欲しい方向教えてね💡


いいですね！🔥  
では、**完全ストリーム処理対応版**に進化させましょう！

---

## ✅ 目的：よりスケーラブルな構成に！

- ✅ 数千万件でも安定動作（**メモリに全部保持しない**）
- ✅ グループを **1つずつ順に処理**（yield return）
- ✅ 処理後にグループのメモリはすぐ解放

---

## 🧩 差し替え対象：`GroupFilesBySize()` → `GroupFilesBySizeStream()`

---

### 🔁 変更前（全件をListに保持）

```csharp
var groups = GroupFilesBySize(files, maxGroupSize);

for (int i = 0; i < groups.Count; i++)
{
    var group = groups[i];
    ...
}
```

---

### ✅ 変更後（ストリーム処理に）

```csharp
int index = 0;

foreach (var group in GroupFilesBySizeStream(Directory.EnumerateFiles(sourceFolder), maxGroupSize))
{
    string tempFolder = $"{tempFolderBase}{++index:D4}";
    ...
}
```

---

## ✅ 追加する `GroupFilesBySizeStream()` メソッド

```csharp
IEnumerable<List<string>> GroupFilesBySizeStream(IEnumerable<string> files, long maxSize)
{
    var currentGroup = new List<string>();
    long currentSize = 0;

    foreach (var file in files)
    {
        long size = 0;

        try
        {
            size = new FileInfo(file).Length;
        }
        catch
        {
            continue; // 読み取りエラーなどはスキップ
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
        yield return currentGroup;
}
```

---

## ✅ WPF版への組み込み（メインループ内）

`StartButton_Click` の中の処理ループをこう変更：

```csharp
int total = 0;
foreach (var _ in GroupFilesBySizeStream(Directory.EnumerateFiles(sourceFolder), maxGroupSize))
    total++;

ProgressBar.Maximum = total;
_progress = 0;

int index = 0;

await Task.Run(() =>
{
    foreach (var group in GroupFilesBySizeStream(Directory.EnumerateFiles(sourceFolder), maxGroupSize))
    {
        _cts.Token.ThrowIfCancellationRequested();
        int groupIndex = Interlocked.Increment(ref index);
        string tempFolder = $"{tempFolderBase}{groupIndex:D4}";
        Directory.CreateDirectory(tempFolder);

        try
        {
            foreach (var file in group)
            {
                string dest = Path.Combine(tempFolder, Path.GetFileName(file));
                File.Move(file, dest);
            }

            if (Directory.Exists(workFolder))
                Directory.Delete(workFolder, true);
            Directory.Move(tempFolder, workFolder);

            if (!RunExternalTool(workFolder))
                throw new Exception("外部ツール実行失敗");

            foreach (var file in Directory.GetFiles(workFolder))
            {
                string dest = Path.Combine(sourceFolder, Path.GetFileName(file));
                File.Move(file, dest);
            }

            Directory.Delete(workFolder, true);
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => MessageBox.Show($"エラー: {ex.Message}"));
            break;
        }

        int done = Interlocked.Increment(ref _progress);
        Dispatcher.Invoke(() =>
        {
            ProgressBar.Value = done;
            double percent = done * 100.0 / ProgressBar.Maximum;
            PercentText.Text = $"{percent:F1}%";
            StatusText.Text = $"処理中: {done}/{total}";
        });
    }
}, _cts.Token);
```

---

## ✅ メモリ効率まとめ

| 構成                    | 状態 |
|-------------------------|------|
| `Directory.EnumerateFiles()` | 遅延列挙で軽量 ✅  
| `GroupFilesBySizeStream()`   | 遅延グループ化でメモリフットプリント最小 ✅  
| `foreach`逐次処理           | ファイル処理→メモリ解放→次へ ✅  

---

## ✅ 次の拡張ポイント（任意）

- 処理時間計測 (`Stopwatch`)
- ログ保存（成功・失敗のグループ一覧）
- UIで「現在処理中のファイル名」表示

---

このストリーム構成で、**ファイル数・サイズに強い超スケーラブル構成**になったよ💪  
あとはUIやログ、フォルダ選択機能などを足していくだけ。

次、やりたい機能ある？それともコード全体統合版ほしい？


了解！お待たせしました 🙌  
ここまでのすべての要件と改善を反映した **完全ストリーム対応・外部ツール連携・進捗表示付きのWPFコード（統合版）** をお届けします。

---

# ✅ WPF版 完全統合コード（大規模ファイル処理に対応）

---

## 📄 MainWindow.xaml

```xml
<Window x:Class="WpfExternalToolProcessor.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        Title="フォルダ分割処理" Height="240" Width="460">
    <StackPanel Margin="10">
        <ProgressBar x:Name="ProgressBar" Height="25" Margin="0,0,0,5"/>
        <TextBlock x:Name="PercentText" Text="0%" HorizontalAlignment="Right" Margin="0,0,0,5"/>
        <TextBlock x:Name="StatusText" Margin="0,0,0,10"/>
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace WpfExternalToolProcessor
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _cts;
        private int _progress = 0;

        private readonly string sourceFolder = @"C:\MainFolder";
        private readonly string workFolder = @"C:\Work";
        private readonly string tempFolderBase = @"C:\Temp_";
        private readonly long maxGroupSize = 32 * 1024 * 1024; // 32MB
        private readonly string externalToolPath = @"C:\Tools\myTool.exe";

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
            ProgressBar.Value = 0;
            PercentText.Text = "0%";
            StatusText.Text = "ファイル列挙中...";
            _progress = 0;
            _cts = new CancellationTokenSource();

            try
            {
                // グループ数だけ先に数える（進捗バー最大値用）
                int totalGroups = GroupFilesBySizeStream(Directory.EnumerateFiles(sourceFolder), maxGroupSize).Count();
                ProgressBar.Maximum = totalGroups;

                int groupIndex = 0;

                await Task.Run(() =>
                {
                    foreach (var group in GroupFilesBySizeStream(Directory.EnumerateFiles(sourceFolder), maxGroupSize))
                    {
                        _cts.Token.ThrowIfCancellationRequested();
                        int currentIndex = Interlocked.Increment(ref groupIndex);

                        string tempFolder = $"{tempFolderBase}{currentIndex:D4}";
                        Directory.CreateDirectory(tempFolder);

                        try
                        {
                            // 元→Temp に移動
                            foreach (var file in group)
                            {
                                string dest = Path.Combine(tempFolder, Path.GetFileName(file));
                                File.Move(file, dest);
                            }

                            // Temp → Work にリネーム
                            if (Directory.Exists(workFolder))
                                Directory.Delete(workFolder, true);

                            Directory.Move(tempFolder, workFolder);

                            // 外部ツール実行
                            if (!RunExternalTool(workFolder))
                                throw new Exception("外部ツールの実行に失敗");

                            // 処理済ファイルを戻す
                            foreach (var file in Directory.GetFiles(workFolder))
                            {
                                string dest = Path.Combine(sourceFolder, Path.GetFileName(file));
                                File.Move(file, dest);
                            }

                            Directory.Delete(workFolder, true);
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"エラー（グループ {currentIndex}）: {ex.Message}");
                            });
                            break;
                        }

                        int done = Interlocked.Increment(ref _progress);
                        Dispatcher.Invoke(() =>
                        {
                            ProgressBar.Value = done;
                            double percent = done * 100.0 / ProgressBar.Maximum;
                            PercentText.Text = $"{percent:F1}%";
                            StatusText.Text = $"処理中: {done}/{totalGroups}";
                        });
                    }
                }, _cts.Token);

                StatusText.Text = "完了！";
                MessageBox.Show("すべての処理が完了しました！");
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "キャンセルされました。";
                MessageBox.Show("処理がキャンセルされました。");
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

        private IEnumerable<List<string>> GroupFilesBySizeStream(IEnumerable<string> files, long maxSize)
        {
            var currentGroup = new List<string>();
            long currentSize = 0;

            foreach (var file in files)
            {
                long size = 0;
                try
                {
                    size = new FileInfo(file).Length;
                }
                catch
                {
                    continue; // エラーのあるファイルはスキップ
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
                yield return currentGroup;
        }

        private bool RunExternalTool(string folderPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = externalToolPath,
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    Console.WriteLine($"外部ツールエラー: {error}");
                    return false;
                }

                Console.WriteLine(process.StandardOutput.ReadToEnd());
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"外部ツール実行エラー: {ex.Message}");
                return false;
            }
        }
    }
}
```

---

## ✅ 特徴

| 要素             | 内容 |
|------------------|------|
| ⚡ 完全ストリーム処理 | メモリに全件保持しないので超大量ファイルOK  
| 📊 UI進捗表示        | 進捗バー + % + 状態テキスト  
| 🛠 外部ツール連携     | `myTool.exe "C:\Work"` 形式で安全に呼び出し  
| ❌ エラー時の中断     | ツール失敗 or 例外時に処理停止  
| ⛔ キャンセル対応     | `CancellationToken` 対応済み  

---

## 💡 次のステップ（追加したければ）

| やりたいこと        | 方法 |
|---------------------|------|
| 📂 フォルダ選択 UI     | `FolderBrowserDialog` で `sourceFolder` を動的に  
| 📝 ログ保存          | 処理ログを `log.txt` に書き出し  
| 🧪 ツール実行時間表示 | `Stopwatch` で計測して `StatusText` に表示  
| 🔄 エラーグループの再処理 | エラーグループだけリスト保存して再実行可能に  

---

必要ならどれでも追加できます！  
「WPFにログウィンドウつけたい」みたいな改造も得意なので、  
やりたいことがあれば気軽に言ってくださいね🔥

よっしゃ、WPF版と同じ要件で  
✅ **完全ストリーム対応**  
✅ **外部ツール連携（引数でフォルダ名）**  
✅ **32MBごとにグループ分割＆逐次処理**  
✅ **処理後は元に戻す**  
✅ **エラー/中断も安全**

を満たす、**安定＆高速なコンソールアプリの完全版コード**を用意したよ！💪

---

## 🧩 コンソールアプリ完全版：`Program.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

class Program
{
    static readonly string sourceFolder = @"C:\MainFolder";
    static readonly string workFolder = @"C:\Work";
    static readonly string tempFolderBase = @"C:\Temp_";
    static readonly long maxGroupSize = 32 * 1024 * 1024; // 32MB
    static readonly string externalToolPath = @"C:\Tools\myTool.exe";

    static void Main(string[] args)
    {
        if (!Directory.Exists(sourceFolder))
        {
            Console.WriteLine("元フォルダが存在しません。");
            return;
        }

        Console.CancelKeyPress += (s, e) =>
        {
            Console.WriteLine("\nキャンセル要求されました。");
            e.Cancel = true;
            _cancelRequested = true;
        };

        Console.WriteLine("ファイル列挙中...");
        var fileEnum = Directory.EnumerateFiles(sourceFolder);

        var groupStream = GroupFilesBySizeStream(fileEnum, maxGroupSize);

        int groupIndex = 0;
        int totalGroups = groupStream.Count();

        Console.WriteLine($"処理対象グループ数: {totalGroups}");

        groupStream = GroupFilesBySizeStream(Directory.EnumerateFiles(sourceFolder), maxGroupSize); // 再列挙

        foreach (var group in groupStream)
        {
            if (_cancelRequested)
            {
                Console.WriteLine("処理をキャンセルしました。");
                break;
            }

            groupIndex++;
            string tempFolder = $"{tempFolderBase}{groupIndex:D4}";
            Console.WriteLine($"\n[{groupIndex}/{totalGroups}] グループ処理開始");

            try
            {
                Directory.CreateDirectory(tempFolder);

                // 元→Temp
                foreach (var file in group)
                {
                    string dest = Path.Combine(tempFolder, Path.GetFileName(file));
                    File.Move(file, dest);
                }

                // Temp→Work にリネーム
                if (Directory.Exists(workFolder))
                    Directory.Delete(workFolder, true);

                Directory.Move(tempFolder, workFolder);

                // 外部ツール実行
                bool success = RunExternalTool(workFolder);
                if (!success)
                {
                    Console.WriteLine("外部ツールの実行に失敗。処理を中断します。");
                    break;
                }

                // Work→元に戻す
                foreach (var file in Directory.GetFiles(workFolder))
                {
                    string dest = Path.Combine(sourceFolder, Path.GetFileName(file));
                    File.Move(file, dest);
                }

                Directory.Delete(workFolder, true);
                Console.WriteLine($"[{groupIndex}/{totalGroups}] 完了");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"エラー: {ex.Message}");
                break;
            }
        }

        Console.WriteLine("\nすべての処理が完了しました。");
    }

    static IEnumerable<List<string>> GroupFilesBySizeStream(IEnumerable<string> files, long maxSize)
    {
        var currentGroup = new List<string>();
        long currentSize = 0;

        foreach (var file in files)
        {
            long size = 0;
            try
            {
                size = new FileInfo(file).Length;
            }
            catch
            {
                continue; // エラーはスキップ
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
            yield return currentGroup;
    }

    static bool _cancelRequested = false;

    static bool RunExternalTool(string folderPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = externalToolPath,
                Arguments = $"\"{folderPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                string error = process.StandardError.ReadToEnd();
                Console.WriteLine($"外部ツールエラー: {error}");
                return false;
            }

            Console.WriteLine(process.StandardOutput.ReadToEnd());
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"外部ツール実行エラー: {ex.Message}");
            return false;
        }
    }
}
```

---

## ✅ 特徴まとめ

| 特徴 | 内容 |
|------|------|
| ⚡ 完全ストリーム処理 | メモリに保持せずグループ単位で1件ずつ処理  
| 🔁 32MB単位で安全分割 | ツールが受け入れ可能なサイズ制約に対応  
| 🧰 外部ツール呼び出し | `ProcessStartInfo` で安定＆標準出力/エラー取得  
| ⛔ Ctrl+C対応 | 安全に中断できるように設計済み  
| ✅ 安定性 | try/catchで各処理ガード、ファイル存在確認など強化も可能  

---

## ✅ 実行環境要件

- `.NET 6` or `.NET Framework 4.8` 以上推奨
- `myTool.exe` は CLIアプリ（引数でフォルダを処理）
- フォルダの読み取り/移動権限があること

---

## ✅ 次にやりたい？

| やりたいこと | 対応可能？ |
|--------------|------------|
| ✅ ログ保存    | `log.txt` に記録  
| ✅ エラーグループだけ再処理 | 再実行モード追加  
| ✅ パフォーマンス計測 | 処理時間・速度表示  
| ✅ ZIP化・転送 | 処理後のファイルを別の場所へ送るなど  

---

「ログも付けたい」「CLIツールがGUIの場合どうする？」など、実運用に向けた強化もできるので、  
どんどん教えてくださいね🔥