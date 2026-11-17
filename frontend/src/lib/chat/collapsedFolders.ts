/**
 * Which folders of the saved chats are collapsed: a preference of this browser (`localStorage`, like the theme and the
 * model), not saved on the server.
 */
const KEY = "dnai-collapsed-folders";

export function readCollapsedFolders(): Set<string> {
  try {
    const value: unknown = JSON.parse(window.localStorage.getItem(KEY) ?? "[]");
    return new Set(Array.isArray(value) ? value.filter((id): id is string => typeof id === "string") : []);
  } catch {
    return new Set();
  }
}

/** Stores the collapsed folders that still exist (deleted folders are forgotten). */
export function storeCollapsedFolders(collapsed: ReadonlySet<string>, existing: readonly string[]): void {
  try {
    window.localStorage.setItem(KEY, JSON.stringify(existing.filter((id) => collapsed.has(id))));
  } catch {
    // Storage full or disabled: the folders just open expanded next time.
  }
}
