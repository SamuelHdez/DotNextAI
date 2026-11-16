"use client";

import { Hamburger, makeStyles, Subtitle2, tokens, Tooltip } from "@fluentui/react-components";
import { ErniLogo } from "@/components/brand/ErniLogo";
import { LanguageMenu } from "@/components/preferences/LanguageMenu";
import { ThemeMenu } from "@/components/preferences/ThemeMenu";
import { HEADER_HEIGHT } from "@/components/shell/navBreakpoint";
import { useI18n } from "@/lib/i18n/I18nProvider";

const useStyles = makeStyles({
  header: {
    position: "sticky",
    top: 0,
    zIndex: 1,
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    height: HEADER_HEIGHT,
    paddingInline: tokens.spacingHorizontalL,
    backgroundColor: tokens.colorNeutralBackground1,
    borderBottom: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
  },
  start: { display: "flex", alignItems: "center", alignSelf: "stretch", gap: tokens.spacingHorizontalS },
  lockup: { display: "flex", alignItems: "center", gap: tokens.spacingHorizontalM },
  product: { color: tokens.colorNeutralForeground1 },
  divider: { width: tokens.strokeWidthThin, height: "20px", backgroundColor: tokens.colorNeutralStroke1 },
  end: { display: "flex", alignItems: "center", gap: tokens.spacingHorizontalXS },
});

interface AppHeaderProps {
  /** Whether the navigation is expanded (drives the hamburger tooltip and aria-expanded). */
  navigationOpen: boolean;
  onToggleNavigation: () => void;
}

export function AppHeader({ navigationOpen, onToggleNavigation }: AppHeaderProps) {
  const styles = useStyles();
  const { t } = useI18n();

  return (
    <header className={styles.header}>
      <div className={styles.start}>
        {/* Fluent 2 Nav: the hamburger is the persistent control to expand and collapse Navigation. */}
        <Tooltip content={navigationOpen ? t("nav.collapse") : t("nav.expand")} relationship="label" positioning="below-start">
          <Hamburger onClick={onToggleNavigation} aria-expanded={navigationOpen} />
        </Tooltip>
        <div className={styles.lockup}>
          <ErniLogo height={24} />
          <span className={styles.divider} aria-hidden />
          <Subtitle2 as="p" className={styles.product}>
            {t("app.name")}
          </Subtitle2>
        </div>
      </div>

      <div className={styles.end}>
        <LanguageMenu />
        <ThemeMenu />
      </div>
    </header>
  );
}
