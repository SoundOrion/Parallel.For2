めっちゃ理解ありますね👏  
「レガシー環境も対象」って超現実的だし、`BackgroundWorker` の知識は**今でも重宝される場面があります。**

---

## 🎯 目標：`BackgroundWorker` を使って、  
✅ フォルダ内のファイルを 32MB 単位でグループ化し  
✅ 一時フォルダに移動  
✅ `C:\Work` にリネーム  
✅ 外部ツールを実行（引数に `C:\Work`）  
✅ 処理後、元に戻す  
✅ UI上で進捗・キャンセル可能  

---

# ✅ `BackgroundWorker`版 WPF サンプル

---

## 🧩 MainWindow.xaml

```xml
<Window x:Class="LegacyProcessor.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        Title="レガシー処理" Height="240" Width="460">
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

## 🧩 MainWindow.xaml.cs

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace LegacyProcessor
{
    public partial class MainWindow : Window
    {
        private BackgroundWorker _worker;
        private List<List<string>> _groups;
        private readonly string sourceFolder = @"C:\MainFolder";
        private readonly string workFolder = @"C:\Work";
        private readonly string tempFolderBase = @"C:\Temp_";
        private readonly long maxGroupSize = 32 * 1024 * 1024; // 32MB
        private readonly string externalToolPath = @"C:\Tools\myTool.exe";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
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
            StatusText.Text = "準備中...";

            _groups = GroupFilesBySize(Directory.EnumerateFiles(sourceFolder), maxGroupSize);
            ProgressBar.Maximum = _groups.Count;

            _worker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            _worker.DoWork += Worker_DoWork;
            _worker.ProgressChanged += Worker_ProgressChanged;
            _worker.RunWorkerCompleted += Worker_Completed;

            _worker.RunWorkerAsync();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _worker?.CancelAsync();
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                if (_worker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                string tempFolder = $"{tempFolderBase}{i + 1:D4}";
                Directory.CreateDirectory(tempFolder);

                try
                {
                    // Move files to temp
                    foreach (var file in _groups[i])
                    {
                        string dest = Path.Combine(tempFolder, Path.GetFileName(file));
                        File.Move(file, dest);
                    }

                    if (Directory.Exists(workFolder))
                        Directory.Delete(workFolder, true);

                    Directory.Move(tempFolder, workFolder);

                    if (!RunExternalTool(workFolder))
                        throw new Exception("外部ツールの実行に失敗");

                    foreach (var file in Directory.GetFiles(workFolder))
                    {
                        string dest = Path.Combine(sourceFolder, Path.GetFileName(file));
                        File.Move(file, dest);
                    }

                    Directory.Delete(workFolder, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"エラー（グループ {i + 1}）: {ex.Message}");
                    e.Cancel = true;
                    return;
                }

                _worker.ReportProgress((i + 1) * 100 / _groups.Count, i + 1);
            }
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            int groupIndex = (int)e.UserState;
            ProgressBar.Value = groupIndex;
            PercentText.Text = $"{e.ProgressPercentage}%";
            StatusText.Text = $"処理中: {groupIndex}/{_groups.Count}";
        }

        private void Worker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                StatusText.Text = "キャンセルされました。";
                MessageBox.Show("処理はキャンセルされました。");
            }
            else if (e.Error != null)
            {
                StatusText.Text = "エラー発生";
                MessageBox.Show($"例外: {e.Error.Message}");
            }
            else
            {
                StatusText.Text = "完了！";
                MessageBox.Show("すべての処理が完了しました！");
            }

            StartButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
        }

        private List<List<string>> GroupFilesBySize(IEnumerable<string> files, long maxSize)
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

## ✅ 特徴まとめ（BackgroundWorker版）

| 機能 | 実装状況 |
|------|----------|
| ✅ 分割・移動・戻し | 完全対応 |
| ✅ 外部ツール実行 | `Process.Start()` |
| ✅ 進捗バー更新 | `ProgressChanged` |
| ✅ キャンセル対応 | `CancelAsync()` |
| ✅ UI連動          | ボタン制御 & テキスト更新 |

---

## ✅ `Task/async` vs `BackgroundWorker` 比較

| 項目 | `BackgroundWorker` | `Task/async` |
|------|---------------------|--------------|
| 非同期基本機能 | 内蔵（イベント式） | 柔軟で直感的（await） |
| UI連携 | `ProgressChanged` | `Dispatcher.Invoke` や `IProgress<T>` |
| 拡張性 | 弱め | 高 |
| レガシー対応 | ◎ | △ |
| 今後の推奨 | ❌（非推奨） | ✅（公式推奨） |

---

## 💬 まとめ

- レガシー環境ならこの `BackgroundWorker` 版でOK 💯  
- 新規開発や将来を見据えるなら `async/await + Task + CancellationToken` がベスト

---

もしこの `BackgroundWorker` 版をもっと進化させたい（ログ、タイマー、UI改善など）ならいつでも言ってね！  
あと、同じ処理を **Windows Forms** でやる版もすぐ出せます🔥

素晴らしい判断！💪  
それじゃあ、上記の **「バッチ処理・一時フォルダ分割・キャンセル対応付きの WPFアプリ」** を  
`BackgroundWorker` ベースに書き換えた**完全サンプル**を提供します！

---

## ✅ `BackgroundWorker`版の特徴：

| 項目 | 内容 |
|------|------|
| ✅ バッチ処理（1000件ずつ）対応  
| ✅ フォルダを 100MBごとに分割して一時フォルダへ移動  
| ✅ `ProgressBar` 連動  
| ✅ キャンセル機能あり  
| ✅ フォルダ構成： `C:\Source`, `C:\Source_00001` ～ `_NNNNN`

---

## 🧩 MainWindow.xaml（そのままでOK）

```xml
<Window x:Class="WpfFileBatchProcessor.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        Title="WPF バッチファイル処理" Height="240" Width="420">
    <StackPanel Margin="10">
        <ProgressBar x:Name="ProgressBar" Height="25" Minimum="0" Margin="0,0,0,5"/>
        <TextBlock x:Name="StatusText" Margin="0,0,0,10"/>
        <Button x:Name="StartButton" Content="開始" Click="StartButton_Click" Margin="0,0,0,5"/>
        <Button x:Name="CancelButton" Content="キャンセル" Click="CancelButton_Click" IsEnabled="False"/>
    </StackPanel>
</Window>
```

---

## 🧩 MainWindow.xaml.cs（BackgroundWorkerで再構築）

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace WpfFileBatchProcessor
{
    public partial class MainWindow : Window
    {
        private BackgroundWorker _worker;
        private List<List<string>> _allGroups;
        private readonly string sourceFolder = @"C:\Source";
        private readonly string tempFolderFormat = @"C:\Source_{0:D5}";
        private readonly long maxGroupSize = 100 * 1024 * 1024;
        private readonly int batchSize = 1000;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(sourceFolder))
            {
                MessageBox.Show("元フォルダが存在しません。");
                return;
            }

            StartButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            StatusText.Text = "準備中...";
            ProgressBar.Value = 0;

            _allGroups = GroupFilesBySize(Directory.EnumerateFiles(sourceFolder), maxGroupSize);
            ProgressBar.Maximum = _allGroups.Count;

            _worker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            _worker.DoWork += Worker_DoWork;
            _worker.ProgressChanged += Worker_ProgressChanged;
            _worker.RunWorkerCompleted += Worker_Completed;

            _worker.RunWorkerAsync();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_worker != null && _worker.IsBusy)
                _worker.CancelAsync();
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            var groupedBatches = Grouped(_allGroups, batchSize).ToList();

            for (int b = 0; b < groupedBatches.Count; b++)
            {
                if (_worker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                var batch = groupedBatches[b];

                PrepareTempFolders(batch.Count, b * batchSize);

                for (int i = 0; i < batch.Count; i++)
                {
                    if (_worker.CancellationPending)
                    {
                        e.Cancel = true;
                        return;
                    }

                    var group = batch[i];
                    int globalIndex = b * batchSize + i;
                    string tempFolder = string.Format(tempFolderFormat, globalIndex + 1);
                    Directory.CreateDirectory(tempFolder);

                    try
                    {
                        foreach (var file in group)
                        {
                            string dest = Path.Combine(tempFolder, Path.GetFileName(file));
                            File.Move(file, dest);
                        }

                        ProcessFolder(tempFolder);

                        foreach (var file in Directory.GetFiles(tempFolder))
                        {
                            string dest = Path.Combine(sourceFolder, Path.GetFileName(file));
                            File.Move(file, dest);
                        }

                        Directory.Delete(tempFolder, true);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"エラー（グループ {globalIndex + 1}）: {ex.Message}");
                        e.Cancel = true;
                        return;
                    }

                    _worker.ReportProgress(globalIndex + 1, globalIndex + 1);
                }
            }
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            int done = (int)e.UserState;
            ProgressBar.Value = done;
            StatusText.Text = $"処理中: {done}/{_allGroups.Count}";
        }

        private void Worker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            StartButton.IsEnabled = true;
            CancelButton.IsEnabled = false;

            if (e.Cancelled)
            {
                StatusText.Text = "キャンセルされました。";
                MessageBox.Show("処理はキャンセルされました。");
            }
            else if (e.Error != null)
            {
                StatusText.Text = "エラー発生";
                MessageBox.Show($"例外: {e.Error.Message}");
            }
            else
            {
                StatusText.Text = "処理完了！";
                MessageBox.Show("すべての処理が完了しました！");
            }
        }

        private List<List<string>> GroupFilesBySize(IEnumerable<string> files, long maxSize)
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

        private void PrepareTempFolders(int count, int offset)
        {
            for (int i = 0; i < count; i++)
            {
                string path = string.Format(tempFolderFormat, i + offset + 1);
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
                Directory.CreateDirectory(path);
            }
        }

        private void ProcessFolder(string folderPath)
        {
            foreach (var file in Directory.GetFiles(folderPath))
            {
                // 仮処理
                Thread.Sleep(10);
            }
        }

        private IEnumerable<List<T>> Grouped<T>(List<T> source, int size)
        {
            for (int i = 0; i < source.Count; i += size)
            {
                yield return source.GetRange(i, Math.Min(size, source.Count - i));
            }
        }
    }
}
```

---

## ✅ 完全対応している機能

| 機能                 | 対応状況 ✅ |
|----------------------|------------|
| フォルダ分割（100MB） | ✅  
| 一時フォルダ作成      | ✅  
| バッチ処理（1000件） | ✅  
| 進捗表示（件数）     | ✅  
| キャンセルボタン     | ✅  
| エラー処理           | ✅  

---

## 🔧 補足

- `GroupFilesBySize()` でグループ化したあとに `Grouped()` でバッチ単位に分けてる
- `PrepareTempFolders()` で一時フォルダ生成
- `ProcessFolder()` に本処理を差し込めば、画像処理や外部ツール呼び出しに変更可能

---

## 💬 まとめ

この `BackgroundWorker` 版は、  
- .NET Framework 環境に対応  
- UIスレッドとバックグラウンドスレッドを完全に分離  
- 小規模〜中規模まで問題なく安定して動作

---

📌 次にやるなら：
- 外部ツール呼び出し対応に変更する？
- ログ機能追加する？
- サブフォルダ対応や ZIP 圧縮などの処理？

必要なら即対応できますよ🔥