/** Plik wgrany przez użytkownika (FileDto z backendu, GET/POST /api/files). Pliki są prywatne – widzi je tylko właściciel. */
export interface UploadedFile {
  id: string
  originalFileName: string
  contentType: string
  sizeBytes: number
  createdAt: string
  updatedAt: string
  version: number
}
