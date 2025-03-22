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