# SWOQ ReplayViewer

This is the ReplayViewer for the Sioux Weekend of Quest (SWOQ), the challenge for the 2025 edition of the Sioux Weekend of Code. With this application, the replay files (with `.swoq` extension) can be opened and viewed.

Using the Load button, you can open a replay file. The application also supports drag and drop of replay files.

## Command-line

The application supports two additional ways of starting from the command line.

- `--watch <folder>`: The given folder is watched for changes. If a new replay file is created, it is automatically loaded. When a replay file is updated, the application automatically scrolls to the end.
- `<file>`: Loads the given file directly.

## Controls

The replay can be viewed using the controls at the bottom:
- The `Play/Pause` button will automatically increment the tick so you can watch the game as a movie.
- The box with up and down arrows allows manually editing the tick value or increasing/decreasing it by one. When the control has focus, you can also use the up and down arrow keys on your keyboard or your mouse scroll wheel as well.
- The progress bar allows fast navigation through the replay. When this control has focus, the arrow keys on your keyboard can also control the position. In addition, you can use Page Up/Down to make larger jumps and Home/End to jump to the start and end, respectively.
