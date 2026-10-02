import { vi, describe, it, expect, beforeEach, afterEach } from "vitest";
import { getApiErrorMessage } from "src/shared/apiError";

const mockApi = vi.hoisted(() => ({
  get: vi.fn(),
}));

vi.mock("boot/axios", () => ({
  api: mockApi,
}));

import {
  DOWNLOAD_TIMEOUT_MS,
  downloadResourceTypeFile,
  REVOKE_DELAY_MS,
} from "src/shared/fileDownload";

interface FakeLink {
  href: string;
  download: string;
  style: { display: string };
  click: ReturnType<typeof vi.fn>;
  remove: ReturnType<typeof vi.fn>;
}

describe("downloadResourceTypeFile", () => {
  let link: FakeLink;
  const createObjectURL = vi.fn(() => "blob:fake-url");
  const revokeObjectURL = vi.fn();
  const appendChild = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    link = {
      href: "",
      download: "",
      style: { display: "" },
      click: vi.fn(),
      remove: vi.fn(),
    };
    vi.stubGlobal("document", {
      createElement: vi.fn(() => link),
      body: { appendChild },
    });
    URL.createObjectURL = createObjectURL;
    URL.revokeObjectURL = revokeObjectURL;
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  const file = { id: 7, resourceTypeId: 3, fileName: "instruks.pdf" };

  it("henter filen via api som blob og laster ned med riktig filnavn", async () => {
    const blob = new Blob(["innhold"], { type: "application/pdf" });
    mockApi.get.mockResolvedValue({ data: blob });

    await downloadResourceTypeFile(file);

    expect(mockApi.get).toHaveBeenCalledWith("/api/resourcetypes/3/files/7", {
      responseType: "blob",
      signal: expect.any(AbortSignal),
    });
    expect(createObjectURL).toHaveBeenCalledWith(blob);
    expect(link.href).toBe("blob:fake-url");
    expect(link.download).toBe("instruks.pdf");
    expect(appendChild).toHaveBeenCalledWith(link);
    expect(link.click).toHaveBeenCalledOnce();
    expect(link.remove).toHaveBeenCalledOnce();
  });

  it("sender med eget timeout-signal med lang grense", async () => {
    const timeout = vi.spyOn(AbortSignal, "timeout");
    mockApi.get.mockResolvedValue({ data: new Blob(["x"]) });

    await downloadResourceTypeFile(file);

    expect(timeout).toHaveBeenCalledWith(DOWNLOAD_TIMEOUT_MS);
    const [, config] = mockApi.get.mock.calls[0] as [
      string,
      { signal: unknown }
    ];
    expect(config.signal).toBe(timeout.mock.results[0]?.value);
    timeout.mockRestore();
  });

  it("frigjør object-URL-en etter en kort forsinkelse", async () => {
    mockApi.get.mockResolvedValue({ data: new Blob(["x"]) });

    await downloadResourceTypeFile(file);

    expect(revokeObjectURL).not.toHaveBeenCalled();
    vi.advanceTimersByTime(REVOKE_DELAY_MS);
    expect(revokeObjectURL).toHaveBeenCalledWith("blob:fake-url");
  });

  it("gjør ProblemDetails i blob-body lesbar for getApiErrorMessage", async () => {
    const problem = {
      title: "Forbidden",
      status: 403,
      detail: "Ingen tilgang.",
    };
    const error = {
      response: {
        status: 403,
        data: new Blob([JSON.stringify(problem)], {
          type: "application/problem+json",
        }),
      },
    };
    mockApi.get.mockRejectedValue(error);

    await expect(downloadResourceTypeFile(file)).rejects.toBe(error);

    expect(error.response.data).toEqual(problem);
    expect(getApiErrorMessage(error, "Klarte ikke å hente filen.")).toBe(
      "Ingen tilgang."
    );
    expect(createObjectURL).not.toHaveBeenCalled();
  });

  it("gir fallback-melding ved nettverksfeil", async () => {
    const error = new Error("Network Error");
    mockApi.get.mockRejectedValue(error);

    await expect(downloadResourceTypeFile(file)).rejects.toBe(error);
    expect(getApiErrorMessage(error, "Klarte ikke å hente filen.")).toBe(
      "Klarte ikke å hente filen."
    );
  });
});
