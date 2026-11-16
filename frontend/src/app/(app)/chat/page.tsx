import type { Metadata } from "next";
import { ChatView } from "@/components/chat/ChatView";
import { getRequestLocale } from "@/lib/i18n/server";
import { translate } from "@/lib/i18n/translate";

export async function generateMetadata(): Promise<Metadata> {
  return { title: translate(await getRequestLocale(), "chat.title") };
}

export default function ChatPage() {
  return <ChatView />;
}
