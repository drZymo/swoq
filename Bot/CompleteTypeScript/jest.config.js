/** jest.config.js */
module.exports = {
    preset: "ts-jest", // use ts-jest for TypeScript files
    testEnvironment: "node", // or 'jsdom' if you're testing browser code
    roots: ["<rootDir>/src"], // look for tests in the src folder

    // Recognize these file extensions:
    moduleFileExtensions: ["ts", "tsx", "js", "jsx", "json", "node"],

    // Use ts-jest to transpile TypeScript files
    transform: {
        "^.+\\.(ts|tsx)$": "ts-jest",
    },

    // This regex tells Jest to look for test files with either .test or .spec in the filename.
    testRegex: "(/__tests__/.*|(\\.|/)(test|spec))\\.tsx?$",

    // Explicitly tell ts-jest which tsconfig to use (optional if it’s in the same folder)
    globals: {
        "ts-jest": {
            tsconfig: "<rootDir>/tsconfig.json",
        },
    },
};
