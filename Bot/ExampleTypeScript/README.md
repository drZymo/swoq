# SWOQ Bot starter - TypeScript version

This is a starter kit to write your own bot for the Sioux Weekend of Quest,
using TypeScript and NodeJS.

## Getting started

-   Install NodeJS (tested with version 22.13).
-   Clone this repo
-   Run `npm install`
-   Copy `example.env` to `.env` and edit its contents.
-   Run `npm start`

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
