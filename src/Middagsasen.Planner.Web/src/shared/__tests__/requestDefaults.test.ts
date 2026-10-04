import { describe, it, expect, vi } from "vitest";
import { AxiosHeaders, type InternalAxiosRequestConfig } from "axios";
import {
  applyRequestDefaults,
  DEFAULT_REQUEST_TIMEOUT_MS,
} from "@/shared/requestDefaults";

function config(
  extra: Partial<InternalAxiosRequestConfig> = {}
): InternalAxiosRequestConfig {
  return { headers: new AxiosHeaders(), ...extra };
}

describe("applyRequestDefaults", () => {
  it("setter standard timeout-signal når kallet ikke har eget signal", () => {
    const timeout = vi.spyOn(AbortSignal, "timeout");

    const result = applyRequestDefaults(config(), null);

    expect(timeout).toHaveBeenCalledWith(DEFAULT_REQUEST_TIMEOUT_MS);
    expect(result.signal).toBe(timeout.mock.results[0]?.value);
    timeout.mockRestore();
  });

  it("lar et eksisterende signal stå", () => {
    const signal = new AbortController().signal;

    const result = applyRequestDefaults(config({ signal }), null);

    expect(result.signal).toBe(signal);
  });

  it("legger på Bearer-token når det finnes", () => {
    const result = applyRequestDefaults(config(), "abc");

    expect(result.headers.Authorization).toBe("Bearer abc");
  });

  it("lar Authorization være uten token", () => {
    const result = applyRequestDefaults(config(), null);

    expect(result.headers.Authorization).toBeUndefined();
  });
});
