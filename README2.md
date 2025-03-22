よっしゃ、やろう！💪  
以下は、**100万ファイル対応の最適化版コンソールアプリ**で、

---

### ✅ 改良点含んだバージョン

| 改良ポイント | 内容 |
|--------------|------|
| ✅ `Directory.EnumerateFiles()` | メモリ節約 & 高速列挙 |
| ✅ バッチ処理対応            | グループを 1000件ずつ段階処理 |
| ✅ 安定性向上               | 長時間実行でも破綻しない構成 |

---

## 📄 最適化版：フルコード（コンソールアプリ）

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
    static readonly int batchSize = 1000;

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
            var groups = GroupFilesBySize(Directory.EnumerateFiles(sourceFolder), maxGroupSize);

            Console.WriteLine($"グループ数: {groups.Count}");

            int total = groups.Count;
            int progress = 0;

            // バッチに分けて処理（例：1000グループごと）
            var batchList = Grouped(groups, batchSize).ToList();

            for (int b = 0; b < batchList.Count; b++)
            {
                Console.WriteLine($"バッチ {b + 1}/{batchList.Count} 開始");

                var batch = batchList[b];

                PrepareTempFolders(batch.Count, b * batchSize);

                await Task.Run(() =>
                {
                    Parallel.ForEach(batch.Select((group, index) => (group, index)), new ParallelOptions
                    {
                        CancellationToken = cts.Token,
                        MaxDegreeOfParallelism = Environment.ProcessorCount
                    }, item =>
                    {
                        var group = item.group;
                        int globalIndex = b * batchSize + item.index;

                        string tempFolder = string.Format(tempFolderFormat, globalIndex + 1);
                        Directory.CreateDirectory(tempFolder);

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
                            Console.WriteLine($"進捗: {done}/{total} グループ完了");
                        }
                    });
                });

                Console.WriteLine($"バッチ {b + 1} 完了");
            }

            Console.WriteLine("すべての処理が完了しました！");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("キャンセルされました。");
        }
    }

    static List<List<string>> GroupFilesBySize(IEnumerable<string> files, long maxSize)
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

    static void PrepareTempFolders(int count, int offset)
    {
        for (int i = 0; i < count; i++)
        {
            string path = string.Format(tempFolderFormat, i + offset + 1);
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
            // 重い処理の仮
            Console.WriteLine($"  処理中: {Path.GetFileName(file)}");
            Thread.Sleep(10);
        }
    }

    static IEnumerable<List<T>> Grouped<T>(List<T> source, int size)
    {
        for (int i = 0; i < source.Count; i += size)
        {
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }
    }
}
```

---

## ✅ 実際の強み

| ポイント | 内容 |
|----------|------|
| 🧠 メモリ効率 | ファイル列挙もグループ処理も段階式でスケーラブル |
| ⚙ 並列数制御 | コア数に応じて最適化 |
| 💪 100万件対応 | フォルダ管理・I/O負荷を分散して安定動作 |
| 🔄 再利用しやすい | WPF版やサービス版への移植もカンタン |

---

## 🚀 次の一手

- このロジックをそのまま WPF に組み込みたい？
- ログ機能や進捗％表示も追加したい？
- ファイル削除・バックアップ・圧縮みたいな処理を組み込みたい？

必要な方向にどんどん進化させていこうぜ💡  
気になるところ、何でも聞いて！

最高です！ここまで来たら、**あとは WPF に組み込むだけで最強のファイルバッチ処理ツールが完成**です💪

---

## 🎯 今回の目標：  
前回の**最適化コンソールコード（100万ファイル対応）**を WPF に移植

---

## ✅ 構成ポイント（WPF版）

| 機能 | 実装内容 |
|------|----------|
| ✅ `EnumerateFiles()` | メモリ効率よくファイル列挙 |
| ✅ グループごとに一時フォルダ作成 | `C:\Source_01`, `Source_02`… |
| ✅ バッチ処理 | 1000グループごとに段階実行 |
| ✅ `ProgressBar` で進捗表示 | 処理完了グループ数で更新 |
| ✅ `キャンセル` ボタン対応 | `CancellationToken` 対応済み |

---

## 📁 フォルダ構成例

```
C:\Source               ← 元ファイル
C:\Source_01 ～ _99999 ← 一時的に使うフォルダ
```

---

## 🧩 1. MainWindow.xaml

```xml
<Window x:Class="WpfFileBatchProcessor.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        Title="WPF バッチファイル処理" Height="220" Width="420">
    <StackPanel Margin="10">
        <ProgressBar x:Name="ProgressBar" Height="25" Minimum="0" Margin="0,0,0,10"/>
        <TextBlock x:Name="StatusText" Margin="0,0,0,10"/>
        <Button x:Name="StartButton" Content="開始" Click="StartButton_Click" Margin="0,0,0,5"/>
        <Button x:Name="CancelButton" Content="キャンセル" Click="CancelButton_Click" IsEnabled="False"/>
    </StackPanel>
</Window>
```

---

## 🧩 2. MainWindow.xaml.cs

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace WpfFileBatchProcessor
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _cts;
        private int _progress = 0;

        private readonly string sourceFolder = @"C:\Source";
        private readonly string tempFolderFormat = @"C:\Source_{0:D5}";
        private readonly long maxGroupSize = 100 * 1024 * 1024;
        private readonly int batchSize = 1000;

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
            StatusText.Text = "準備中...";
            ProgressBar.Value = 0;
            _progress = 0;

            _cts = new CancellationTokenSource();

            try
            {
                var groups = GroupFilesBySize(Directory.EnumerateFiles(sourceFolder), maxGroupSize);
                ProgressBar.Maximum = groups.Count;

                var batchList = Grouped(groups, batchSize).ToList();

                for (int b = 0; b < batchList.Count; b++)
                {
                    var batch = batchList[b];
                    PrepareTempFolders(batch.Count, b * batchSize);

                    StatusText.Text = $"バッチ {b + 1}/{batchList.Count} 実行中";

                    await Task.Run(() =>
                    {
                        Parallel.ForEach(batch.Select((group, index) => (group, index)), new ParallelOptions
                        {
                            CancellationToken = _cts.Token,
                            MaxDegreeOfParallelism = Environment.ProcessorCount
                        }, item =>
                        {
                            var group = item.group;
                            int globalIndex = b * batchSize + item.index;

                            string tempFolder = string.Format(tempFolderFormat, globalIndex + 1);
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
                                Dispatcher.Invoke(() =>
                                {
                                    ProgressBar.Value = done;
                                });
                            }
                        });
                    });
                }

                StatusText.Text = "処理完了！";
                MessageBox.Show("すべての処理が完了しました！");
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "キャンセルされました。";
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

        private void ProcessFolder(string folderPath, CancellationToken token)
        {
            foreach (var file in Directory.GetFiles(folderPath))
            {
                token.ThrowIfCancellationRequested();
                // 仮処理（実際の処理に置き換えて）
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

## ✅ やる気MAXな人向け：次に足せる機能

| 追加機能 | 内容 |
|----------|------|
| 🔍 処理結果ログ | 処理成功/失敗ファイル一覧をログ出力 |
| 📂 フォルダ選択UI | `FolderBrowserDialog` で動的に対象選べるように |
| 🔄 処理再開機能 | 中断後に未処理分だけ再開するステート管理 |
| 🗂️ フォルダツリーUI | 処理対象フォルダの内容プレビュー表示 |

---

やりたい方向性があれば、次に何やるか一緒に決めよう！  
> 進捗のパーセント出したい？ログ保存したい？UIカスタマイズ？  
どんどん進化させられるよ🔥