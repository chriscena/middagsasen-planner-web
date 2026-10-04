import { describe, it, expect } from "vitest";
import { ApprovalFilter, ApprovalStatus } from "src/types";
import {
  approvalAction,
  buildWorkHourPatch,
  countBulkApprovalError,
  filterIgnoresSeason,
  getApprovalActionText,
  getApprovalStatusDisplay,
  getApprovedByText,
  getWorkHourChanges,
  getWorkHourError,
  getWorkHourErrorKind,
  hasDuration,
  isOpen,
  parseApprovalFilter,
  seasonForQuery,
  summarizeBulkApproval,
} from "src/shared/workHours";

const original = {
  startDateTime: "2026-01-10T08:00:00.000Z",
  endDateTime: "2026-01-10T12:00:00.000Z",
  description: "Preparering",
};

describe("isOpen", () => {
  it("er åpen uten status", () => {
    expect(isOpen(null)).toBe(true);
    expect(isOpen(undefined)).toBe(true);
  });

  it("er ikke åpen når godkjent eller avslått", () => {
    expect(isOpen(ApprovalStatus.Approved)).toBe(false);
    expect(isOpen(ApprovalStatus.Rejected)).toBe(false);
  });
});

describe("getApprovalStatusDisplay", () => {
  it("viser godkjent", () => {
    expect(getApprovalStatusDisplay(ApprovalStatus.Approved)).toEqual({
      icon: "check_circle",
      iconClass: "green-text",
      badgeColor: "positive",
      label: "Godkjent",
    });
  });

  it("viser avslått som «Avslått»", () => {
    expect(getApprovalStatusDisplay(ApprovalStatus.Rejected)).toEqual({
      icon: "cancel",
      iconClass: "red-text",
      badgeColor: "negative",
      label: "Avslått",
    });
  });

  it("viser åpen (null/undefined) som «Ubehandlet» uten badge", () => {
    const open = {
      icon: "radio_button_unchecked",
      iconClass: "grey-text",
      badgeColor: null,
      label: "Ubehandlet",
    };
    expect(getApprovalStatusDisplay(null)).toEqual(open);
    expect(getApprovalStatusDisplay(undefined)).toEqual(open);
  });

  it("viser ukjent status (f.eks. eldre rader med 0) som ubehandlet", () => {
    const unknown = 0 as ApprovalStatus;
    expect(getApprovalStatusDisplay(unknown)).toEqual(
      getApprovalStatusDisplay(null)
    );
    expect(isOpen(unknown)).toBe(true);
    expect(
      getApprovedByText({
        approvedBy: 1,
        approvedByName: "Ola",
        approvalStatus: unknown,
      })
    ).toBe("");
  });
});

describe("getApprovalActionText", () => {
  it("har tekster for godkjenning og avslag", () => {
    expect(getApprovalActionText(ApprovalStatus.Approved)).toEqual({
      noun: "godkjenning",
      past: "godkjent",
      done: "Timeføring godkjent",
    });
    expect(getApprovalActionText(ApprovalStatus.Rejected)).toEqual({
      noun: "avslag",
      past: "avslått",
      done: "Timeføring avslått",
    });
  });
});

describe("getApprovedByText", () => {
  it("navngir hvem som godkjente eller avslo", () => {
    expect(
      getApprovedByText({
        approvedBy: 3,
        approvedByName: "Kari",
        approvalStatus: ApprovalStatus.Approved,
      })
    ).toBe("Godkjent av: Kari");
    expect(
      getApprovedByText({
        approvedBy: 3,
        approvedByName: null,
        approvalStatus: ApprovalStatus.Rejected,
      })
    ).toBe("Avslått av: ukjent");
  });

  it("er tom for åpne føringer eller uten godkjenner", () => {
    expect(getApprovedByText({ approvedBy: 3, approvalStatus: null })).toBe("");
    expect(getApprovedByText({ approvalStatus: ApprovalStatus.Approved })).toBe(
      ""
    );
  });
});

describe("filteret og sesongregelen", () => {
  it("tolker filteret fra URL-en", () => {
    expect(parseApprovalFilter("1")).toBe(ApprovalFilter.Approved);
    expect(parseApprovalFilter("2")).toBe(ApprovalFilter.Rejected);
    expect(parseApprovalFilter("3")).toBe(ApprovalFilter.Pending);
    expect(parseApprovalFilter("0")).toBe(ApprovalFilter.All);
  });

  it("faller tilbake til Ubehandlet for manglende eller ugyldig verdi", () => {
    expect(parseApprovalFilter(undefined)).toBe(ApprovalFilter.Pending);
    expect(parseApprovalFilter("tull")).toBe(ApprovalFilter.Pending);
    expect(parseApprovalFilter("7")).toBe(ApprovalFilter.Pending);
    expect(parseApprovalFilter(null, ApprovalFilter.All)).toBe(
      ApprovalFilter.All
    );
  });

  it("ubehandlede føringer ignorerer sesongfilteret", () => {
    expect(filterIgnoresSeason(ApprovalFilter.Pending)).toBe(true);
    expect(seasonForQuery(ApprovalFilter.Pending, 2025)).toBeNull();
  });

  it("andre filtre bruker sesongen", () => {
    for (const filter of [
      ApprovalFilter.All,
      ApprovalFilter.Approved,
      ApprovalFilter.Rejected,
    ]) {
      expect(filterIgnoresSeason(filter)).toBe(false);
      expect(seasonForQuery(filter, 2025)).toBe(2025);
    }
    expect(seasonForQuery(ApprovalFilter.Approved, null)).toBeNull();
  });
});

describe("hasDuration", () => {
  it("krever mer enn 0:00", () => {
    expect(
      hasDuration("2026-01-10T08:00:00.000Z", "2026-01-10T09:00:00.000Z")
    ).toBe(true);
    expect(
      hasDuration("2026-01-10T08:00:00.000Z", "2026-01-10T08:00:20.000Z")
    ).toBe(false);
    expect(hasDuration(null, "2026-01-10T08:00:00.000Z")).toBe(false);
    expect(
      hasDuration("2026-01-10T08:00:00.000Z", "2026-01-10T08:00:30.000Z")
    ).toBe(true);
    expect(
      hasDuration("2026-01-10T09:00:00.000Z", "2026-01-10T08:00:00.000Z")
    ).toBe(false);
  });
});

describe("getWorkHourChanges", () => {
  it("returns empty diff for unchanged form", () => {
    expect(getWorkHourChanges(original, { ...original })).toEqual({});
  });

  it("compares times by instant, not string format", () => {
    const current = {
      ...original,
      startDateTime: "2026-01-10T08:00:00Z",
      endDateTime: "2026-01-10T13:00:00.000+01:00",
    };
    expect(getWorkHourChanges(original, current)).toEqual({});
  });

  it("returns only endTime when end time changed", () => {
    const current = { ...original, endDateTime: "2026-01-10T13:00:00.000Z" };
    expect(getWorkHourChanges(original, current)).toEqual({
      endTime: "2026-01-10T13:00:00.000Z",
    });
  });

  it("returns only startTime when start time changed", () => {
    const current = { ...original, startDateTime: "2026-01-10T07:30:00.000Z" };
    expect(getWorkHourChanges(original, current)).toEqual({
      startTime: "2026-01-10T07:30:00.000Z",
    });
  });

  it("returns only description when description changed", () => {
    const current = { ...original, description: "Heiskjøring" };
    expect(getWorkHourChanges(original, current)).toEqual({
      description: "Heiskjøring",
    });
  });

  it("treats null and empty description as equal", () => {
    expect(
      getWorkHourChanges(
        { ...original, description: null },
        { ...original, description: "" }
      )
    ).toEqual({});
  });
});

describe("buildWorkHourPatch", () => {
  it("approve without changes sends only approvalStatus", () => {
    expect(
      buildWorkHourPatch(original, { ...original }, ApprovalStatus.Approved)
    ).toEqual({ approvalStatus: ApprovalStatus.Approved });
  });

  it("reject without changes sends only approvalStatus Rejected", () => {
    expect(
      buildWorkHourPatch(original, { ...original }, ApprovalStatus.Rejected)
    ).toEqual({ approvalStatus: ApprovalStatus.Rejected });
  });

  it("changed description + approve sends description and approvalStatus", () => {
    const current = { ...original, description: "Ny tekst" };
    expect(
      buildWorkHourPatch(original, current, ApprovalStatus.Approved)
    ).toEqual({
      description: "Ny tekst",
      approvalStatus: ApprovalStatus.Approved,
    });
  });

  it("without approval returns only changed fields", () => {
    const current = { ...original, endDateTime: "2026-01-10T14:00:00.000Z" };
    expect(buildWorkHourPatch(original, current)).toEqual({
      endTime: "2026-01-10T14:00:00.000Z",
    });
  });

  it("sends no approvalStatus when status is null", () => {
    expect(buildWorkHourPatch(original, { ...original }, null)).toEqual({});
  });

  it("never includes userId", () => {
    const withUser = { ...original, userId: 1 };
    const current = {
      startDateTime: "2026-01-10T09:00:00.000Z",
      endDateTime: "2026-01-10T15:00:00.000Z",
      description: "Endret",
      userId: 2,
    };
    const patch = buildWorkHourPatch(
      withUser,
      current,
      ApprovalStatus.Approved
    );
    expect(patch).not.toHaveProperty("userId");
    expect(Object.keys(patch).sort()).toEqual(
      ["approvalStatus", "description", "endTime", "startTime"].sort()
    );
  });
});

describe("getWorkHourErrorKind", () => {
  it("maps 409 to conflict", () => {
    expect(getWorkHourErrorKind({ response: { status: 409 } })).toBe(
      "conflict"
    );
  });
  it("maps 403 to forbidden", () => {
    expect(getWorkHourErrorKind({ response: { status: 403 } })).toBe(
      "forbidden"
    );
  });
  it("maps 404 to notFound", () => {
    expect(getWorkHourErrorKind({ response: { status: 404 } })).toBe(
      "notFound"
    );
  });
  it("maps other errors to other", () => {
    expect(getWorkHourErrorKind({ response: { status: 500 } })).toBe("other");
    expect(getWorkHourErrorKind(new Error("network"))).toBe("other");
    expect(getWorkHourErrorKind(undefined)).toBe("other");
  });
});

describe("getWorkHourError", () => {
  const problem = (status: number, detail?: string) => ({
    response: {
      status,
      data: { title: "Feil", status, ...(detail ? { detail } : {}) },
    },
  });

  it("409 gir «allerede behandlet» og ny lasting", () => {
    expect(getWorkHourError(problem(409), "update")).toEqual({
      message: "Føringen er allerede behandlet og kan ikke endres lenger",
      kind: "conflict",
      shouldReload: true,
    });
  });

  it("409 ved statusendring har egen melding", () => {
    expect(getWorkHourError(problem(409), "changeStatus").message).toBe(
      "Statusen kunne ikke endres fordi føringen er endret av noen andre"
    );
  });

  it("404 gir «finnes ikke lenger» og ny lasting", () => {
    expect(getWorkHourError(problem(404), "delete")).toEqual({
      message: "Føringen finnes ikke lenger",
      kind: "notFound",
      shouldReload: true,
    });
  });

  it("403 gir tilgangsmelding uten ny lasting", () => {
    expect(getWorkHourError(problem(403), "approve")).toEqual({
      message: "Du har ikke tilgang til å endre denne føringen",
      kind: "forbidden",
      shouldReload: false,
    });
  });

  it("andre feil gir standardteksten for handlingen", () => {
    expect(getWorkHourError(problem(500), "update")).toEqual({
      message: "Klarte ikke å lagre endringer",
      kind: "other",
      shouldReload: false,
    });
    expect(getWorkHourError(new Error("nett"), "delete").message).toBe(
      "Klarte ikke å slette timeføring"
    );
    expect(getWorkHourError(undefined, "approve").message).toBe(
      "Klarte ikke å godkjenne timeføring"
    );
    expect(getWorkHourError(undefined, "reject").message).toBe(
      "Klarte ikke å avslå timeføring"
    );
    expect(getWorkHourError(undefined, "changeStatus").message).toBe(
      "Klarte ikke å oppdatere status"
    );
  });

  it("ProblemDetails `detail` fra backend vinner", () => {
    expect(
      getWorkHourError(problem(409, "Føringen er låst"), "update")
    ).toEqual({
      message: "Føringen er låst",
      kind: "conflict",
      shouldReload: true,
    });
    expect(
      getWorkHourError(problem(400, "Sluttid må være etter starttid"), "update")
        .message
    ).toBe("Sluttid må være etter starttid");
  });

  it("opprettelse bruker bare standardteksten og laster aldri på nytt", () => {
    expect(getWorkHourError(problem(409), "create")).toEqual({
      message: "Klarte ikke å lagre timer",
      kind: "conflict",
      shouldReload: false,
    });
    expect(
      getWorkHourError(problem(400, "Overlapper annen føring"), "create")
        .message
    ).toBe("Overlapper annen føring");
  });

  it("approvalAction gir handlingen for statusen", () => {
    expect(approvalAction(ApprovalStatus.Approved)).toBe("approve");
    expect(approvalAction(ApprovalStatus.Rejected)).toBe("reject");
  });
});

describe("countBulkApprovalError", () => {
  it("teller etter feiltype", () => {
    const counts = { ok: 0, alreadyProcessed: 0, notFound: 0, failed: 0 };
    countBulkApprovalError(counts, { response: { status: 409 } });
    countBulkApprovalError(counts, { response: { status: 404 } });
    countBulkApprovalError(counts, { response: { status: 403 } });
    countBulkApprovalError(counts, new Error("nett"));
    expect(counts).toEqual({
      ok: 0,
      alreadyProcessed: 1,
      notFound: 1,
      failed: 2,
    });
  });
});

describe("summarizeBulkApproval", () => {
  it("is positive when all succeeded", () => {
    expect(
      summarizeBulkApproval(
        { ok: 8, alreadyProcessed: 0, failed: 0 },
        ApprovalStatus.Approved
      )
    ).toEqual({ type: "positive", message: "8 godkjent" });
  });

  it("reports already processed as warning", () => {
    expect(
      summarizeBulkApproval(
        { ok: 8, alreadyProcessed: 2, failed: 0 },
        ApprovalStatus.Approved
      )
    ).toEqual({
      type: "warning",
      message: "8 godkjent, 2 var allerede behandlet",
    });
  });

  it("reports failures and uses avslått for rejection", () => {
    expect(
      summarizeBulkApproval(
        { ok: 3, alreadyProcessed: 1, failed: 1 },
        ApprovalStatus.Rejected
      )
    ).toEqual({
      type: "warning",
      message: "3 avslått, 1 var allerede behandlet, 1 feilet",
    });
  });

  it("reports entries that no longer exist separately", () => {
    expect(
      summarizeBulkApproval(
        { ok: 5, alreadyProcessed: 1, notFound: 2, failed: 0 },
        ApprovalStatus.Approved
      )
    ).toEqual({
      type: "warning",
      message: "5 godkjent, 1 var allerede behandlet, 2 fantes ikke lenger",
    });
  });
});
