import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it } from "vitest";
import { LanguageMenu } from "@/components/preferences/LanguageMenu";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { LOCALE_COOKIE } from "@/lib/i18n/locales";
import { renderWithProviders } from "@/test/render";

afterEach(() => {
  document.cookie = `${LOCALE_COOKIE}=; max-age=0; path=/`;
});

function HomeLabel() {
  const { t } = useI18n();
  return <p>{t("nav.home")}</p>;
}

describe("LanguageMenu", () => {
  it("switches the whole UI to Spanish and remembers it", async () => {
    const user = userEvent.setup();
    renderWithProviders(
      <>
        <LanguageMenu />
        <HomeLabel />
      </>,
    );

    await user.click(screen.getByRole("button", { name: "EN · Language: English" }));
    await user.click(screen.getByRole("menuitemradio", { name: "Español" }));

    expect(screen.getByText("Inicio")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "ES · Idioma: Español" })).toBeInTheDocument();
    expect(document.cookie).toContain(`${LOCALE_COOKIE}=es`);
  });

  it("names every language in its own language", async () => {
    const user = userEvent.setup();
    renderWithProviders(<LanguageMenu />, { locale: "es" });

    await user.click(screen.getByRole("button", { name: "ES · Idioma: Español" }));

    expect(screen.getByRole("menuitemradio", { name: "English" })).toHaveAttribute("lang", "en");
    expect(screen.getByRole("menuitemradio", { name: "Español" })).toBeChecked();
  });
});
