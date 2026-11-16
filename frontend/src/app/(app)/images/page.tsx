import type { Metadata } from "next";
import { ImagesView } from "@/components/images/ImagesView";
import { getRequestLocale } from "@/lib/i18n/server";
import { translate } from "@/lib/i18n/translate";

export async function generateMetadata(): Promise<Metadata> {
  return { title: translate(await getRequestLocale(), "images.title") };
}

export default function ImagesPage() {
  return <ImagesView />;
}
