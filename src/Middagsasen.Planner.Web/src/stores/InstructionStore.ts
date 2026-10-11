// Instrukser (vedlegg som krever kvittering) for innlogget bruker: listen fra
// GET api/me/instructions, kvittering og telleren til badgen i menyen.
import { defineStore } from "pinia";
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
  loading: boolean;
}

export const useInstructionStore = defineStore("instructions", {
  state: (): InstructionState => ({
    groups: [],
    loading: false,
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
    async getMyInstructions(): Promise<void> {
      this.loading = true;
      try {
        const response = await api.get<InstructionGroupResponse[]>(
          "/api/me/instructions"
        );
        this.groups = response.data;
      } finally {
        this.loading = false;
      }
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
        await this.getMyInstructions();
      } catch (error) {
        console.error(error);
      }
      return response.data;
    },
  },
});
