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
  // Løpenummer for siste startede henting. Bare den får sette groups og
  // nullstille loading; svar fra eldre hentinger forkastes.
  fetchSeq: number;
  // Pågående henting, delt av parallelle getMyInstructions()-kall. markRaw,
  // så promiset ikke pakkes inn i en proxy.
  inFlight: Promise<void> | null;
}

export const useInstructionStore = defineStore("instructions", {
  state: (): InstructionState => ({
    groups: [],
    loading: false,
    fetchSeq: 0,
    inFlight: null,
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
      return this.inFlight ?? this.reloadInstructions();
    },
    // Starter alltid en ny henting og gjør pågående uaktuelle. Brukes etter
    // kvittering, der et svar som alt er underveis kan være fra før den.
    reloadInstructions(): Promise<void> {
      const seq = ++this.fetchSeq;
      this.loading = true;
      const load = async (): Promise<void> => {
        const response = await api.get<InstructionGroupResponse[]>(
          "/api/me/instructions"
        );
        if (seq === this.fetchSeq) this.groups = response.data;
      };
      // finally-callbacken kjører alltid asynkront, så inFlight er satt før
      // den nullstilles.
      const request = load().finally(() => {
        if (seq !== this.fetchSeq) return;
        this.loading = false;
        this.inFlight = null;
      });
      this.inFlight = markRaw(request);
      return request;
    },
    // Ved utlogging: tøm listen og forkast svar som er underveis.
    reset(): void {
      this.fetchSeq += 1;
      this.inFlight = null;
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
