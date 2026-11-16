import { Desktop20Regular, WeatherMoon20Regular, WeatherSunny20Regular } from "@fluentui/react-icons";
import type { ReactElement } from "react";
import type { MessageKey } from "@/lib/i18n/messages/en";
import type { ThemePreference } from "@/lib/theme/themePreference";

export const THEME_OPTIONS: { value: ThemePreference; label: MessageKey; icon: ReactElement }[] = [
  { value: "system", label: "theme.system", icon: <Desktop20Regular /> },
  { value: "light", label: "theme.light", icon: <WeatherSunny20Regular /> },
  { value: "dark", label: "theme.dark", icon: <WeatherMoon20Regular /> },
];

export function themeOption(value: ThemePreference) {
  return THEME_OPTIONS.find((option) => option.value === value) ?? THEME_OPTIONS[0];
}
