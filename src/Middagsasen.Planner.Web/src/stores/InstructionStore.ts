// Instrukser (vedlegg som krever kvittering) for innlogget bruker: listen fra
// GET api/me/instructions, kvittering og telleren til badgen i menyen.
import { defineStore } from "pinia";
import { markRaw } from "vue";
import { api } from "@/boot/axios";
import type {
  AcknowledgeRequest,
  FileInfoResponse,
  InstructionGroupResponse,
} from "@/types";

interface InstructionState {
  // Aktive instrukser gruppert per vakttype, med appliesToMe og
  // kvitteringsstatus (acknowledgedAt) for innlogget bruker.
  groups: InstructionGroupResponse[];
  // Sann fra en henting starter til den siste startede er ferdig.
  loading: boolean;
  // Løpenummer for siste startede henting. Bare den får nullstille loading.
  // reset() øker det også.
  fetchSeq: number;
  // Løpenummeret til svaret som sist ble lagt i groups. Et svar brukes bare
  // hvis det er nyere enn dette, så et vellykket eldre svar gir data til et
  // nyere kommer (også om den nyeste hentingen feiler), mens et eldre svar
  // aldri overskriver et nyere. reset() setter det til fetchSeq, så alle
  // svar som er underveis da forkastes.
  appliedSeq: number;
  // Siste startede henting, også etter at den er ferdig (null før første
  // henting og etter reset()). Det er den som avgjør utfallet for kallerne
  // (se waitForLatest). Pågår den (loading), deles den av getMyInstructions. markRaw, så promiset ikke pakkes inn i en
  // proxy; ligger i staten (ikke i en modulvariabel) så hver Pinia-instans
  // (og hver test) har sin egen.
  latestFetch: Promise<void> | null;
}

// Starter en henting og gjør den til latestFetch. Bare for store-ens egne
// actions (getMyInstructions/reloadInstructions), som venter på riktig
// henting med waitForLatest.
function startFetch(state: InstructionState): void {
  const seq = ++state.fetchSeq;
  state.loading = true;
  const load = async (): Promise<void> => {
    const response = await api.get<InstructionGroupResponse[]>(
      "/api/me/instructions"
    );
    if (seq > state.appliedSeq) {
      state.groups = response.data;
      state.appliedSeq = seq;
    }
  };
  const request = load().finally(() => {
    if (seq === state.fetchSeq) state.loading = false;
  });
  // Feilen leveres til kallerne via waitForLatest; dette hindrer bare
  // «unhandled rejection» for en henting ingen lenger venter på.
  request.catch(() => {});
  state.latestFetch = markRaw(request);
}

// Venter til siste startede henting er ferdig, og gir utfallet av den.
// Startes en nyere henting mens man venter, følges den i stedet, så kalleren
// aldri får feilen (eller suksessen) fra en henting som er gjort uaktuell.
// Har en eldre henting lyktes og den nyeste feilet, får kalleren feilen,
// mens groups beholder dataene fra den eldre. Etter reset() uten ny henting
// finnes det ingenting å vente på; da løses promiset uten å kaste (listen er
// tømt).
async function waitForLatest(state: InstructionState): Promise<void> {
  for (;;) {
    const request = state.latestFetch;
    if (!request) return;
    try {
      await request;
    } catch (error) {
      if (request === state.latestFetch) throw error;
      continue;
    }
    if (request === state.latestFetch) return;
  }
}

export const useInstructionStore = defineStore("instructions", {
  state: (): InstructionState => ({
    groups: [],
    loading: false,
    fetchSeq: 0,
    appliedSeq: 0,
    latestFetch: null,
  }),
  getters: {
    // Antall uleste instrukser på vakttyper som gjelder brukeren (badgen).
    unacknowledgedCount: (state): number =>
      state.groups
        .filter((group) => group.appliesToMe)
        .flatMap((group) => group.files)
        .filter((file) => !file.acknowledgedAt).length,
  },
  actions: {
    // Henter listen. Pågår en henting alt, deles den, så MainLayout og
    // Instrukser-siden gir én GET ved oppstart.
    getMyInstructions(): Promise<void> {
      if (!this.loading) startFetch(this);
      return waitForLatest(this);
    },
    // Starter alltid en ny henting og gjør pågående uaktuelle. Brukes etter
    // kvittering og andre endringer som påvirker listen (vakter, vedlegg), der
    // et svar som alt er underveis kan være fra før endringen. Også kallere
    // som venter på en eldre henting får utfallet av denne.
    reloadInstructions(): Promise<void> {
      startFetch(this);
      return waitForLatest(this);
    },
    // Ved utlogging og bytte av bruker: tøm listen og forkast svar som er
    // underveis.
    reset(): void {
      this.fetchSeq += 1;
      this.appliedSeq = this.fetchSeq;
      this.latestFetch = null;
      this.groups = [];
      this.loading = false;
    },
    // Kvitterer for innlogget bruker. Tom kommentar sendes ikke (ingen body).
    // Idempotent i backend. Listen hentes på nytt etterpå så badgen teller
    // ned; kvitteringen har lyktes uansett, så feil i hentingen logges og
    // svelges.
    async acknowledge(
      resourceTypeId: number,
      fileId: number,
      comment?: string | null
    ): Promise<FileInfoResponse> {
      const trimmed = comment?.trim();
      const body: AcknowledgeRequest | undefined = trimmed
        ? { comment: trimmed }
        : undefined;
      const response = await api.post<FileInfoResponse>(
        `/api/resourcetypes/${resourceTypeId}/files/${fileId}/acknowledgements`,
        body
      );
      try {
        await this.reloadInstructions();
      } catch (error) {
        console.error(error);
      }
      return response.data;
    },
  },
});

// Henter instrukslisten på nytt i bakgrunnen etter en endring som kan
// påvirke den (vakt tatt/trukket, vedlegg endret). Kalleren venter ikke, og
// feil logges bare: badgen er ikke kritisk, og selve endringen har lyktes.
export function reloadInstructionsInBackground(): void {
  useInstructionStore()
    .reloadInstructions()
    .catch((error: unknown) => console.error(error));
}
