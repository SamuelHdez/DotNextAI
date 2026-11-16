"use client";

import { Button, Menu, MenuItemRadio, MenuList, MenuPopover, MenuTrigger, Tooltip } from "@fluentui/react-components";
import { Globe20Regular } from "@fluentui/react-icons";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { LOCALES, type Locale } from "@/lib/i18n/locales";

/** Language selector: the choice is stored in a cookie and applied at once, without a navigation. */
export function LanguageMenu() {
  const { locale, setLocale, t } = useI18n();
  // The accessible name starts with the visible code ("EN · Language: English"), so voice control users can say what
  // they see (WCAG 2.5.3, Label in Name; axe matches whole words, so the code is not wrapped in punctuation).
  const code = locale.toUpperCase();
  const label = t("prefs.languageCurrent", { language: t(`language.${locale}`), code });

  return (
    <Menu
      positioning="below-end"
      checkedValues={{ language: [locale] }}
      onCheckedValueChange={(_, { checkedItems }) => setLocale(checkedItems[0] as Locale)}
    >
      <Tooltip content={label} relationship="label" withArrow>
        <MenuTrigger disableButtonEnhancement>
          <Button appearance="subtle" icon={<Globe20Regular />}>
            {code}
          </Button>
        </MenuTrigger>
      </Tooltip>
      <MenuPopover>
        <MenuList>
          {LOCALES.map((option) => (
            // Each language is named in its own language, so it can be found whatever the current one is.
            <MenuItemRadio key={option} name="language" value={option} lang={option}>
              {t(`language.${option}`)}
            </MenuItemRadio>
          ))}
        </MenuList>
      </MenuPopover>
    </Menu>
  );
}
