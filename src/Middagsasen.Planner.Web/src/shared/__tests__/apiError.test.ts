import { describe, it, expect } from "vitest";
import { AxiosError, AxiosHeaders } from "axios";
import {
  getApiErrorMessage,
  getErrorResponse,
  isProblemDetails,
} from "src/shared/apiError";

function problem(status: number, detail: string | null | undefined) {
  return {
    type: "https://tools.ietf.org/html/rfc9110#section-15.5.5",
    title: "Some title",
    status,
    detail,
    traceId: "00-abc",
  };
}

function axiosError(status: number, data: unknown) {
  const config = { headers: new AxiosHeaders() };
  return new AxiosError("Request failed", "ERR_BAD_REQUEST", config, null, {
    status,
    statusText: "",
    headers: {},
    config,
    data,
  });
}

describe("getErrorResponse", () => {
  it("returns response from axios-like errors", () => {
    expect(getErrorResponse({ response: { status: 404 } })).toEqual({
      status: 404,
    });
  });
  it("returns undefined without response", () => {
    expect(getErrorResponse(new Error("x"))).toBeUndefined();
    expect(getErrorResponse(undefined)).toBeUndefined();
    expect(getErrorResponse({ response: null })).toBeUndefined();
  });
});

describe("isProblemDetails", () => {
  it("accepts ProblemDetails bodies", () => {
    expect(isProblemDetails(problem(404, ""))).toBe(true);
    expect(isProblemDetails({ title: "Forbidden", status: 403 })).toBe(true);
  });
  it("rejects other values", () => {
    expect(isProblemDetails("Ugyldig telefonnummer")).toBe(false);
    expect(isProblemDetails(null)).toBe(false);
    expect(isProblemDetails([])).toBe(false);
    expect(isProblemDetails({ foo: "bar" })).toBe(false);
    expect(isProblemDetails({ status: "404" })).toBe(false);
  });
});

describe("getApiErrorMessage", () => {
  it("returns ProblemDetails detail", () => {
    const error = axiosError(
      409,
      problem(409, "Timeføringen er allerede behandlet og kan ikke endres.")
    );
    expect(getApiErrorMessage(error, "fallback")).toBe(
      "Timeføringen er allerede behandlet og kan ikke endres."
    );
  });

  it("works with plain axios-like objects", () => {
    const error = {
      response: {
        status: 403,
        data: problem(
          403,
          "Du har ikke tilgang til å utføre denne handlingen."
        ),
      },
    };
    expect(getApiErrorMessage(error, "fallback")).toBe(
      "Du har ikke tilgang til å utføre denne handlingen."
    );
  });

  it("returns fallback for empty, blank or missing detail", () => {
    expect(
      getApiErrorMessage(axiosError(404, problem(404, "")), "fallback")
    ).toBe("fallback");
    expect(
      getApiErrorMessage(axiosError(404, problem(404, "  ")), "fallback")
    ).toBe("fallback");
    expect(
      getApiErrorMessage(axiosError(403, problem(403, null)), "fallback")
    ).toBe("fallback");
    expect(
      getApiErrorMessage(
        axiosError(403, { title: "Forbidden", status: 403 }),
        "fallback"
      )
    ).toBe("fallback");
  });

  it("returns fallback for 5xx", () => {
    expect(
      getApiErrorMessage(
        axiosError(500, problem(500, "An unexpected error occurred.")),
        "fallback"
      )
    ).toBe("fallback");
    expect(getApiErrorMessage(axiosError(502, "Bad Gateway"), "fallback")).toBe(
      "fallback"
    );
  });

  it("returns first message from ValidationProblemDetails", () => {
    const error = axiosError(400, {
      type: "https://tools.ietf.org/html/rfc9110#section-15.5.1",
      title: "One or more validation errors occurred.",
      status: 400,
      errors: {
        StartTime: ["The StartTime field is required.", "Second"],
        EndTime: ["The EndTime field is required."],
      },
    });
    expect(getApiErrorMessage(error, "fallback")).toBe(
      "The StartTime field is required."
    );
  });

  it("returns fallback for ValidationProblemDetails without messages", () => {
    const error = axiosError(400, {
      title: "One or more validation errors occurred.",
      status: 400,
      errors: { StartTime: [] },
    });
    expect(getApiErrorMessage(error, "fallback")).toBe("fallback");
  });

  it("returns plain string body", () => {
    expect(
      getApiErrorMessage(axiosError(400, "Ugyldig telefonnummer"), "fallback")
    ).toBe("Ugyldig telefonnummer");
    expect(getApiErrorMessage(axiosError(400, "  "), "fallback")).toBe(
      "fallback"
    );
    expect(getApiErrorMessage(axiosError(403, ""), "fallback")).toBe(
      "fallback"
    );
  });

  it("ignores legacy { error } bodies", () => {
    expect(
      getApiErrorMessage(
        axiosError(409, { error: "Gammel melding" }),
        "fallback"
      )
    ).toBe("fallback");
  });

  it("returns fallback for network errors without response", () => {
    const error = new AxiosError("Network Error", "ERR_NETWORK");
    expect(getApiErrorMessage(error, "fallback")).toBe("fallback");
  });

  it("returns fallback for non-axios errors", () => {
    expect(getApiErrorMessage(new TypeError("boom"), "fallback")).toBe(
      "fallback"
    );
    expect(getApiErrorMessage(undefined, "fallback")).toBe("fallback");
    expect(getApiErrorMessage("oops", "fallback")).toBe("fallback");
  });
});
