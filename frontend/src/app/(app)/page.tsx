import type { Metadata } from "next";
import { HomeView } from "@/components/home/HomeView";
import { getRequestLocale } from "@/lib/i18n/server";
import { translate } from "@/lib/i18n/translate";

export async function generateMetadata(): Promise<Metadata> {
  return { title: translate(await getRequestLocale(), "nav.home") };
}

export default function HomePage() {
  return <HomeView />;
}
