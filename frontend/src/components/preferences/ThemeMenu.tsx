"use client";

import { Button, Menu, MenuItemRadio, MenuList, MenuPopover, MenuTrigger, Tooltip } from "@fluentui/react-components";
import { THEME_OPTIONS, themeOption } from "@/components/preferences/themeOptions";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { useThemePreference, type ThemePreference } from "@/lib/theme/themePreference";

/** Light, dark or follow the operating system (stored in localStorage, applied before first paint). */
export function ThemeMenu() {
  const { t } = useI18n();
  const [theme, setTheme] = useThemePreference();
  const current = themeOption(theme);
  const label = t("prefs.appearanceCurrent", { theme: t(current.label) });

  return (
    <Menu
      positioning="below-end"
      checkedValues={{ theme: [theme] }}
      onCheckedValueChange={(_, { checkedItems }) => setTheme(checkedItems[0] as ThemePreference)}
    >
      <Tooltip content={label} relationship="label" withArrow>
        <MenuTrigger disableButtonEnhancement>
          <Button appearance="subtle" icon={current.icon} />
        </MenuTrigger>
      </Tooltip>
      <MenuPopover>
        <MenuList>
          {THEME_OPTIONS.map((option) => (
            <MenuItemRadio key={option.value} name="theme" value={option.value} icon={option.icon}>
              {t(option.label)}
            </MenuItemRadio>
          ))}
        </MenuList>
      </MenuPopover>
    </Menu>
  );
}
