import { vi, describe, it, expect, beforeEach, afterEach } from "vitest";

const mockNotify = vi.hoisted(() => ({ create: vi.fn() }));

vi.mock("quasar", () => ({
  Notify: mockNotify,
}));

import { notifyApiError } from "src/shared/notifyApiError";

function apiError(status: number, url: string, data?: unknown) {
  return Object.assign(new Error("Request failed"), {
    config: { url },
    response: { status, data },
  });
}

describe("notifyApiError", () => {
  let consoleError: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    vi.clearAllMocks();
    consoleError = vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("varsler ikke ved utløpt sesjon (401 fra vanlig endepunkt)", () => {
    notifyApiError(apiError(401, "/api/events"), "Fallback");

    expect(mockNotify.create).not.toHaveBeenCalled();
    expect(consoleError).not.toHaveBeenCalled();
  });

  it("varsler ved 401 fra autentiseringsendepunkt", () => {
    const error = apiError(401, "/api/authentication/authenticate", {
      status: 401,
      detail: "Feil brukernavn eller passord.",
    });

    notifyApiError(error, "Fallback");

    expect(mockNotify.create).toHaveBeenCalledWith({
      type: "negative",
      message: "Feil brukernavn eller passord.",
    });
  });

  it("viser ProblemDetails.detail som rødt varsel og logger feilen", () => {
    const error = apiError(400, "/api/events", {
      type: "https://tools.ietf.org/html/rfc9110#section-15.5.1",
      title: "Bad Request",
      status: 400,
      detail: "Vakta er allerede tatt.",
    });

    notifyApiError(error, "Fallback");

    expect(consoleError).toHaveBeenCalledWith(error);
    expect(mockNotify.create).toHaveBeenCalledTimes(1);
    expect(mockNotify.create).toHaveBeenCalledWith({
      type: "negative",
      message: "Vakta er allerede tatt.",
    });
  });

  it("viser fallback ved nettverksfeil", () => {
    const error = new Error("Network Error");

    notifyApiError(error, "Klarte ikke å hente vaktlista.");

    expect(consoleError).toHaveBeenCalledWith(error);
    expect(mockNotify.create).toHaveBeenCalledWith({
      type: "negative",
      message: "Klarte ikke å hente vaktlista.",
    });
  });
});
