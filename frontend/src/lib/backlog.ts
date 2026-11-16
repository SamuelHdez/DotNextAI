import type { MessageKey } from "@/lib/i18n/messages/en";

export type StepStatus = "done" | "current" | "planned";

export interface BacklogStep {
  number: string;
  title: MessageKey;
  status: StepStatus;
}

export interface BacklogPhase {
  title: MessageKey;
  steps: BacklogStep[];
}

/**
 * Course backlog shown on the home page. Keep it in sync with docs/specs/product-rules.md#backlog and
 * docs/tasks/todo.md when a step starts or ends.
 */
export const BACKLOG: BacklogPhase[] = [
  {
    title: "backlog.phase1",
    steps: [
      { number: "01", title: "backlog.step01", status: "done" },
      { number: "02", title: "backlog.step02", status: "done" },
      { number: "03", title: "backlog.step03", status: "done" },
      { number: "04", title: "backlog.step04", status: "done" },
    ],
  },
  {
    title: "backlog.phase2",
    steps: [
      { number: "05", title: "backlog.step05", status: "done" },
      { number: "06", title: "backlog.step06", status: "done" },
      { number: "07", title: "backlog.step07", status: "done" },
    ],
  },
  {
    title: "backlog.phase3",
    steps: [
      { number: "08", title: "backlog.step08", status: "done" },
      { number: "09", title: "backlog.step09", status: "done" },
      { number: "10", title: "backlog.step10", status: "planned" },
    ],
  },
];
