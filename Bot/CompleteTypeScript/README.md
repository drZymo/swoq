# SWOQ Bot example - TypeScript version

This is an example bot that can play all levels except the last one of the
Sioux Weekend of Quest. It was written to find potential issues in the server
code and overall game development flow during the development of the quest
itself.

It was built in a similar spirit as what would (probably) be done during the
weekend itself, i.e. just start somewhere, add new features as new proto files
become available and hack things together until it (somewhat) works.

If you are brave enough too look inside, you'll find lots of ugliness, at least
two different approaches to tackle common goal coordination, some
level-specific `if`'s (contrary to what I wanted to do), etc.

It is, quite frankly, a big mess :)

## Getting started

- Install NodeJS (tested with version 22.13).
- Clone this repo
- Run `npm install`
- Copy `example.env` to `.env` and edit its contents.
- Run `npm start`

## Development

To (re-)generate the generated gRPC client and messages: `npm run proto`

To rebuild the sources once, run `npm run build`.

To keep rebuilding the sources whenever you save them: `npm run watch`.

Tip: if you open this folder in VSCode, it should automatically start
the default build task, which will perform this background automatically
for you.

The entry-point to the bot is `index.ts`.

Happy coding!

## License

MIT
