import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ImagesView } from "@/components/images/ImagesView";
import { checkPrompt, downloadName, MAX_PROMPT_LENGTH } from "@/lib/images/imageRules";
import { PNG_BASE64 } from "@/test/images";
import { jsonResponse, renderWithProviders } from "@/test/render";

let fetchMock: ReturnType<typeof vi.fn>;

beforeEach(() => {
  fetchMock = vi.fn();
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => vi.unstubAllGlobals());

const image = (revisedPrompt: string | null = "A fox reading by lamplight.") =>
  jsonResponse({ mediaType: "image/png", data: PNG_BASE64, model: "gpt-image-2", revisedPrompt });

async function generate(user: ReturnType<typeof userEvent.setup>, prompt: string) {
  await user.type(screen.getByRole("textbox", { name: "Description" }), prompt);
  await user.click(screen.getByRole("button", { name: "Generate" }));
}

describe("ImagesView", () => {
  it("generates an image and shows it with a download link", async () => {
    fetchMock.mockResolvedValue(image());
    const user = userEvent.setup();
    renderWithProviders(<ImagesView />);
    expect(screen.getByText("Your images will appear here.")).toBeInTheDocument();

    await generate(user, "  A fox reading  ");

    const gallery = await screen.findByRole("list", { name: "Images" });
    const img = within(gallery).getByRole("img", { name: "Generated image: A fox reading" });
    expect(img).toHaveAttribute("src", `data:image/png;base64,${PNG_BASE64}`);
    expect(within(gallery).getByText("The model drew: A fox reading by lamplight.")).toBeInTheDocument();
    expect(within(gallery).getByText("gpt-image-2")).toBeInTheDocument();
    const download = within(gallery).getByRole("link", { name: "Download" });
    expect(download).toHaveAttribute("download", "dotnextai-image-1.png");

    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit];
    expect(String(url)).toBe("http://api.test/api/images");
    expect(JSON.parse(init.body as string)).toEqual({ prompt: "A fox reading", size: "square" });
  });

  it("sends the chosen shape", async () => {
    fetchMock.mockResolvedValue(image());
    const user = userEvent.setup();
    renderWithProviders(<ImagesView />);

    await user.click(screen.getByRole("radio", { name: "Landscape" }));
    await generate(user, "A wide valley");
    await screen.findByRole("list", { name: "Images" });

    expect(JSON.parse((fetchMock.mock.calls[0][1] as RequestInit).body as string).size).toBe("landscape");
  });

  it("shows the newest image first", async () => {
    fetchMock.mockResolvedValueOnce(image(null)).mockResolvedValueOnce(image(null));
    const user = userEvent.setup();
    renderWithProviders(<ImagesView />);

    await generate(user, "first");
    await screen.findByRole("img", { name: "Generated image: first" });
    await user.clear(screen.getByRole("textbox", { name: "Description" }));
    await generate(user, "second");
    await screen.findByRole("img", { name: "Generated image: second" });

    const names = within(screen.getByRole("list", { name: "Images" })).getAllByRole("img").map((img) => img.getAttribute("alt"));
    expect(names).toEqual(["Generated image: second", "Generated image: first"]);
  });

  it("shows the failure in the selected language", async () => {
    fetchMock.mockResolvedValue(jsonResponse({ status: 503, failure: "ModelUnavailable", detail: "English" }, 503, "application/problem+json"));
    const user = userEvent.setup();
    renderWithProviders(<ImagesView />, { locale: "es" });

    await user.type(screen.getByRole("textbox", { name: "Descripción" }), "Un zorro");
    await user.click(screen.getByRole("button", { name: "Generar" }));

    expect(await screen.findByText("El modelo no está disponible en este momento. Vuelve a intentarlo en unos minutos.")).toBeInTheDocument();
  });

  it("does not send an empty description", async () => {
    const user = userEvent.setup();
    renderWithProviders(<ImagesView />);

    await user.click(screen.getByRole("button", { name: "Generate" }));

    expect(await screen.findByText("Describe the image first.")).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("shows that it is drawing and disables Generate meanwhile", async () => {
    let finish!: (response: Response) => void;
    fetchMock.mockReturnValue(new Promise<Response>((resolve) => (finish = resolve)));
    const user = userEvent.setup();
    renderWithProviders(<ImagesView />);

    await generate(user, "A slow fox");

    expect(screen.getByText("Drawing the image… this can take up to a minute.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Generate" })).toBeDisabled();
    finish(image());
    expect(await screen.findByRole("img", { name: "Generated image: A slow fox" })).toBeInTheDocument();
  });
});

describe("image rules", () => {
  it("checks the description", () => {
    expect(checkPrompt("  fox ")).toEqual({ ok: true, prompt: "fox" });
    expect(checkPrompt(" ")).toEqual({ ok: false, reason: "empty" });
    expect(checkPrompt("a".repeat(MAX_PROMPT_LENGTH + 1))).toEqual({ ok: false, reason: "tooLong", length: MAX_PROMPT_LENGTH + 1 });
  });

  it("names downloads after the format", () => {
    expect(downloadName(3, "image/png")).toBe("dotnextai-image-3.png");
    expect(downloadName(4, "image/jpeg")).toBe("dotnextai-image-4.jpg");
  });
});
