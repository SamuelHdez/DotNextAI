"use client";

import { Body1, Button, Card, CardFooter, CardHeader, makeStyles, Subtitle1, tokens } from "@fluentui/react-components";
import { ArrowRight20Regular, Chat24Regular, ImageSparkle24Regular } from "@fluentui/react-icons";
import { useRouter } from "next/navigation";
import { BacklogCard } from "@/components/home/BacklogCard";
import { ServiceStatusCard } from "@/components/home/ServiceStatusCard";
import { PageBody, PageHeader } from "@/components/ui/PageLayout";
import { useI18n } from "@/lib/i18n/I18nProvider";

const useStyles = makeStyles({
  grid: { display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))", gap: tokens.spacingHorizontalXL },
  chatIcon: { color: tokens.colorBrandForeground1 },
  description: { color: tokens.colorNeutralForeground2 },
});

/** Dashboard: entry to the chat, service status and the course backlog. */
export function HomeView() {
  const styles = useStyles();
  const { t } = useI18n();
  const router = useRouter();

  return (
    <PageBody>
      <PageHeader title={t("home.title")} subtitle={t("home.subtitle")} />
      <div className={styles.grid}>
        <Card>
          <CardHeader image={<Chat24Regular className={styles.chatIcon} />} header={<Subtitle1 as="h2">{t("home.chat.title")}</Subtitle1>} />
          <Body1 className={styles.description}>{t("home.chat.description")}</Body1>
          <CardFooter>
            <Button
              as="a"
              href="/chat"
              appearance="primary"
              icon={<ArrowRight20Regular />}
              iconPosition="after"
              onClick={(event) => {
                if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
                event.preventDefault();
                router.push("/chat");
              }}
            >
              {t("home.chat.action")}
            </Button>
          </CardFooter>
        </Card>
        <Card>
          <CardHeader
            image={<ImageSparkle24Regular className={styles.chatIcon} />}
            header={<Subtitle1 as="h2">{t("home.images.title")}</Subtitle1>}
          />
          <Body1 className={styles.description}>{t("home.images.description")}</Body1>
          <CardFooter>
            <Button
              as="a"
              href="/images"
              icon={<ArrowRight20Regular />}
              iconPosition="after"
              onClick={(event) => {
                if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
                event.preventDefault();
                router.push("/images");
              }}
            >
              {t("home.images.action")}
            </Button>
          </CardFooter>
        </Card>
        <ServiceStatusCard />
      </div>
      <BacklogCard />
    </PageBody>
  );
}
