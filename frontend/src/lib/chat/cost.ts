import type { Locale } from "@/lib/i18n/locales";

/**
 * A cost in US dollars for the UI language: two significant digits below $1 ("$0.00014", "$0.012", "$0.25") and two
 * decimals from $1 ("$1.23"). Same rule as the CLI's `CliMessages.Cost` (docs/specs/feature-token-metrics.md#formats).
 */
export function formatCost(cost: number, locale: Locale): string {
  const decimals = cost > 0 && cost < 1 ? Math.min(10, Math.max(2, 1 - Math.floor(Math.log10(cost)))) : 2;
  return new Intl.NumberFormat(locale, {
    style: "currency",
    currency: "USD",
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  }).format(cost);
}
