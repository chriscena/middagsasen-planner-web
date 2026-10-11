import { describe, it, expect } from "vitest";
import { attachmentKind, reachedEnd } from "@/shared/readTracking";

describe("attachmentKind", () => {
  it("er pdf for application/pdf uansett filnavn og store bokstaver", () => {
    expect(attachmentKind("application/pdf", "instruks.pdf")).toBe("pdf");
    expect(attachmentKind("Application/PDF", "instruks")).toBe("pdf");
    expect(attachmentKind(" application/pdf ", "fil.bin")).toBe("pdf");
  });

  it("er pdf når filnavnet slutter på .pdf selv med generisk MIME", () => {
    expect(attachmentKind("application/octet-stream", "instruks.pdf")).toBe(
      "pdf"
    );
    expect(attachmentKind("", "INSTRUKS.PDF")).toBe("pdf");
  });

  it("er image for image/*", () => {
    expect(attachmentKind("image/png", "kart.png")).toBe("image");
    expect(attachmentKind("image/jpeg", "kart.jpg")).toBe("image");
    expect(attachmentKind("IMAGE/GIF", "kart.gif")).toBe("image");
  });

  it("er other for alt annet", () => {
    expect(attachmentKind("application/octet-stream", "fil.docx")).toBe(
      "other"
    );
    expect(attachmentKind("text/plain", "notat.txt")).toBe("other");
    expect(attachmentKind("", "")).toBe("other");
    // Bare endelsen .pdf teller, ikke .pdf et annet sted i navnet.
    expect(attachmentKind("", "instruks.pdf.docx")).toBe("other");
  });
});

describe("reachedEnd", () => {
  it("er ikke nådd før sentinelen har vært synlig", () => {
    expect(reachedEnd(false, [])).toBe(false);
    expect(reachedEnd(false, [{ isIntersecting: false }])).toBe(false);
  });

  it("er nådd når sentinelen er synlig", () => {
    expect(
      reachedEnd(false, [{ isIntersecting: false }, { isIntersecting: true }])
    ).toBe(true);
  });

  it("forblir nådd når brukeren blar opp igjen", () => {
    expect(reachedEnd(true, [{ isIntersecting: false }])).toBe(true);
    expect(reachedEnd(true, [])).toBe(true);
  });
});
