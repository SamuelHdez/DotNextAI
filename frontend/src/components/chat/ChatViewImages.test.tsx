import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ChatView } from "@/components/chat/ChatView";
import { PNG_BASE64, pngFile, textFile } from "@/test/images";
import { chatPageFetch, jsonResponse, renderWithProviders, streamedAnswer } from "@/test/render";

let fetchMock: ReturnType<typeof vi.fn>;

beforeEach(() => {
  fetchMock = vi.fn();
  vi.stubGlobal("fetch", chatPageFetch(fetchMock));
});

afterEach(() => vi.unstubAllGlobals());

const answer = (text: string) => streamedAnswer(text);
const sentBodies = () => fetchMock.mock.calls.map(([, init]) => JSON.parse((init as RequestInit).body as string));
const fileInput = () => screen.getByTestId("image-input") as HTMLInputElement;

async function send(user: ReturnType<typeof userEvent.setup>, text: string) {
  await user.type(screen.getByRole("textbox", { name: "Your question" }), text);
  await user.click(screen.getByRole("button", { name: "Send" }));
}

describe("ChatView images", () => {
  it("attaches an image, shows its preview and sends it in base64 with the question", async () => {
    fetchMock.mockResolvedValue(answer("A single pixel."));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await user.upload(fileInput(), pngFile("pixel.png"));
    const attachments = screen.getByRole("list", { name: "Images for your next question" });
    expect(within(attachments).getByRole("img", { name: "Attached image pixel.png" })).toBeInTheDocument();

    await send(user, "What is this?");

    expect(await screen.findByText("A single pixel.")).toBeInTheDocument();
    expect(sentBodies()[0]).toEqual({
      question: "What is this?",
      images: [{ mediaType: "image/png", data: PNG_BASE64 }],
    });
    // Shown in the conversation, gone from the composer.
    expect(within(screen.getByRole("log")).getByRole("img", { name: "Attached image pixel.png" })).toBeInTheDocument();
    expect(screen.queryByRole("list", { name: "Images for your next question" })).not.toBeInTheDocument();
  });

  it("sends images with their question only", async () => {
    fetchMock.mockResolvedValueOnce(answer("A pixel.")).mockResolvedValueOnce(answer("Black."));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await user.upload(fileInput(), pngFile());
    await send(user, "What is this?");
    await screen.findByText("A pixel.");
    await send(user, "What colour?");
    await screen.findByText("Black.");

    expect(sentBodies()[1]).toEqual({ question: "What colour?" });
  });

  it("shows a sent image at full size when it is clicked, and closes it", async () => {
    fetchMock.mockResolvedValue(answer("A pixel."));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await user.upload(fileInput(), pngFile("pixel.png"));
    await send(user, "What is this?");
    await screen.findByText("A pixel.");
    await user.click(within(screen.getByRole("log")).getByRole("button", { name: "Attached image pixel.png" }));

    const viewer = await screen.findByRole("dialog", { name: "Attached image pixel.png" });
    expect(within(viewer).getByRole("img", { name: "Attached image pixel.png" })).toHaveAttribute("src", expect.stringMatching(/^data:image\/png;base64,/));
    await user.click(within(viewer).getByRole("button", { name: "Close" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });

  it("removes an attachment", async () => {
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await user.upload(fileInput(), pngFile("pixel.png"));
    await user.click(screen.getByRole("button", { name: "Remove pixel.png" }));

    expect(screen.queryByRole("list", { name: "Images for your next question" })).not.toBeInTheDocument();
  });

  it("rejects a file that is not an image, in the selected language", async () => {
    const user = userEvent.setup({ applyAccept: false });
    renderWithProviders(<ChatView />, { locale: "es" });

    await user.upload(fileInput(), textFile("notas.png"));

    expect(await screen.findByText("notas.png no es una imagen PNG, JPEG, WebP o GIF.")).toBeInTheDocument();
    expect(screen.queryByRole("list", { name: "Imágenes para tu próxima pregunta" })).not.toBeInTheDocument();
  });

  it("keeps at most four images and says so", async () => {
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await user.upload(fileInput(), [1, 2, 3, 4, 5].map((n) => pngFile(`p${n}.png`)));

    expect(await screen.findByText("You can attach up to 4 images to one question.")).toBeInTheDocument();
    expect(within(screen.getByRole("list", { name: "Images for your next question" })).getAllByRole("img")).toHaveLength(4);
    expect(screen.getByRole("button", { name: "Attach images" })).toBeDisabled();
  });

  it("gives the images back when the question fails", async () => {
    fetchMock.mockResolvedValue(jsonResponse({ status: 504, failure: "Timeout" }, 504, "application/problem+json"));
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await user.upload(fileInput(), pngFile("pixel.png"));
    await send(user, "What is this?");

    expect(await screen.findByText("The model took too long to answer. Try again.")).toBeInTheDocument();
    expect(screen.getByRole("img", { name: "Attached image pixel.png" })).toBeInTheDocument();
  });

  it("attaches a pasted image", async () => {
    const user = userEvent.setup();
    renderWithProviders(<ChatView />);

    await user.click(screen.getByRole("textbox", { name: "Your question" }));
    await user.paste({ files: [pngFile("screenshot.png")] } as unknown as DataTransfer);

    expect(await screen.findByRole("img", { name: "Attached image screenshot.png" })).toBeInTheDocument();
  });
});
