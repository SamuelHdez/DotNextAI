"use client";

import { Button, makeStyles, Menu, MenuItemRadio, MenuList, MenuPopover, MenuTrigger, Tooltip } from "@fluentui/react-components";
import { Globe20Regular } from "@fluentui/react-icons";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { LOCALES, type Locale } from "@/lib/i18n/locales";

const useStyles = makeStyles({
  code: { textTransform: "uppercase" },
});

/** Language selector: the choice is stored in a cookie and applied at once, without a navigation. */
export function LanguageMenu() {
  const styles = useStyles();
  const { locale, setLocale, t } = useI18n();
  const label = t("prefs.languageCurrent", { language: t(`language.${locale}`) });

  return (
    <Menu
      positioning="below-end"
      checkedValues={{ language: [locale] }}
      onCheckedValueChange={(_, { checkedItems }) => setLocale(checkedItems[0] as Locale)}
    >
      <Tooltip content={label} relationship="label" withArrow>
        <MenuTrigger disableButtonEnhancement>
          <Button appearance="subtle" icon={<Globe20Regular />}>
            <span className={styles.code} aria-hidden>
              {locale}
            </span>
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
