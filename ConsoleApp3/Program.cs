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

        using (var cts = new CancellationTokenSource())
        {
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
