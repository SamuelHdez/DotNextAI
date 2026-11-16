/** The model chosen in the selector, remembered in this browser (step 08). */
export const MODEL_STORAGE_KEY = "dnai-model";

export function readModelPreference(): string | null {
  try {
    return window.localStorage.getItem(MODEL_STORAGE_KEY);
  } catch {
    return null;
  }
}

/** Stores the choice; choosing the default forgets it, so a new default on the server applies. */
export function storeModelPreference(model: string, defaultModel: string): void {
  try {
    if (model === defaultModel) window.localStorage.removeItem(MODEL_STORAGE_KEY);
    else window.localStorage.setItem(MODEL_STORAGE_KEY, model);
  } catch {
    // Storage can be unavailable (private mode); the choice still applies to this page.
  }
}

/** The stored choice if the server still offers it, otherwise the default. */
export function initialModel(available: readonly string[], defaultModel: string, stored: string | null): string {
  return stored && available.includes(stored) ? stored : defaultModel;
}
