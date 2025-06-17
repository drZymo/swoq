using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Swoq.ReplayViewer.Util;

public static class FileWatcherObservable
{
    /// <summary>
    /// Creates an observable that emits a FileSystemEventArgs event every time a ".swoq" file is created in the specified folder.
    /// </summary>
    /// <param name="folderPath">The folder to watch.</param>
    /// <returns>An IObservable of FileSystemEventArgs.</returns>
    public static IObservable<FileSystemEventArgs> WatchSwoqFiles(string folderPath)
    {
        return Observable.Create<FileSystemEventArgs>(observer =>
        {
            // Set up FileSystemWatcher to only watch for *.swoq files.
            var watcher = new FileSystemWatcher(folderPath)
            {
                Filter = "*.swoq",
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            // Create an event handler that pushes events to subscribers.
            FileSystemEventHandler onCreated = (sender, eventArgs) =>
            {
                observer.OnNext(eventArgs);
            };

            watcher.Created += onCreated;

            // Return a disposable that detaches the event handler and disposes the watcher when unsubscribed.
            return Disposable.Create(() =>
            {
                watcher.Created -= onCreated;
                watcher.Dispose();
            });
        });
    }
}
