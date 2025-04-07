export function requireEnvVar(name: string): string {
    const result = process.env[name];
    if (!result) {
        console.error(
            `${name} environment variable is required, see README.md`,
        );
        process.exit(1);
    }
    return result;
}

export function sleep(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
}

export function formatDate(date: Date): string {
    const pad = (n: number): string => n.toString().padStart(2, "0");
    const year = date.getFullYear();
    const month = pad(date.getMonth() + 1);
    const day = pad(date.getDate());
    const hours = pad(date.getHours());
    const minutes = pad(date.getMinutes());
    const seconds = pad(date.getSeconds());
    return `${year}${month}${day}-${hours}${minutes}${seconds}`;
}

type Entries<T> = {
    [K in keyof T]: [K, T[K]];
}[keyof T][];

export function objectEntries<T extends object>(obj: T): Entries<T> {
    return Object.entries(obj) as any;
}
