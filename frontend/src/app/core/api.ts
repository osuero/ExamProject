import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface DomainSummary { code: string; name: string; weightPercent: number | null; }
export interface CatalogItem {
  code: string; title: string; level: string; shortDescription: string;
  languages: string[]; languageStatus: 'verified' | 'pending_verification';
  questionCount: number; durationMinutes: number; profileVersion: number; profileVerification: string;
  domains: DomainSummary[];
  availability: { publishedQuestions: number; fullSimulationAvailable: boolean; practiceAvailable: boolean };
}
export interface CatalogDetail {
  code: string; title: string; level: string; shortDescription: string; description: string; disclaimer: string;
  languages: { locale: string; sourceId: string; verifiedAt: string }[];
  languageStatus: string;
  profile: {
    version: number; questionCount: number; examDurationMinutes: number; appointmentDurationMinutes: number | null;
    allowedQuestionTypes: string[]; simulatorPassPercent: number; scoringPolicy: string; officialScoreReference: string | null;
    verificationStatus: string; blueprintVersion: string | null; notes: string | null;
    sources: { id: string; title: string; url: string; kind: string; confidence: string }[];
  };
  domains: (DomainSummary & { objectives: string[]; publishedQuestions: number })[];
  modes: { id: string; name: string; description: string }[];
  availability: { publishedQuestions: number; fullSimulationAvailable: boolean };
}
export interface Me { authenticated: boolean; id?: string; email?: string; role?: 'Student' | 'Admin'; }
export interface Score {
  pointsEarned: number; pointsMax: number; percent: number; correct: number; incorrect: number; omitted: number;
  passed: boolean; passPercent: number; timeUsedSeconds: number; scoringPolicy: string;
}
export interface AttemptSummary {
  id: string; certificationCode: string; mode: string; preview: boolean; status: 'in_progress' | 'submitted' | 'expired';
  classification: 'clean' | 'assisted'; feedbackEnabled: boolean; profileVersion: number;
  startedAt: string; deadlineAt: string | null; finishedAt: string | null; questionCount: number | null; score: Score | null;
}
export interface ItemOption { id: string; label: string; text: string; isCorrect?: boolean; rationale?: string; }
export interface Solution {
  correctOptionIds: string[]; scoredOptionIds: string[]; learningOptionIds: string[] | null; isCorrect: boolean | null;
  explanation: string; sources: { id: string; title: string; url: string }[];
}
export interface AttemptItem {
  position: number; domainCode: string; questionType: 'single_choice' | 'multiple_response'; selectCount: number;
  scenario: { id: string; title: string; text: string } | null; stem: string; options: ItemOption[];
  selected: string[]; flagged: boolean; feedbackRevealed: boolean; solution: Solution | null;
  questionRef: { externalId: string; version: number };
}
export interface AttemptState { attempt: AttemptSummary; serverNow: string; remainingSeconds: number | null; items: AttemptItem[]; }
export interface AttemptResult {
  attempt: AttemptSummary; disclaimer: string;
  byDomain: { code: string; name: string; total: number; correct: number; pointsEarned: number; pointsMax: number }[];
  review: AttemptItem[]; toReinforce: string[];
}
export interface StartRequest {
  certificationCode: string; mode: 'practice' | 'simulation' | 'custom'; feedback?: boolean; questionCount?: number;
  durationMinutes?: number | null; domainCodes?: string[]; preview?: boolean;
}
export interface ProgressDomain { code: string; name: string; answered: number; correct: number; sampleNote: string | null; }
export interface Progress {
  certificationCode: string; note: string;
  clean: { attempts: number; byDomain: ProgressDomain[] };
  assisted: { attempts: number; byDomain: ProgressDomain[] };
  timeline: { id: string; finishedAt: string; mode: string; classification: string; percent: number; pointsEarned: number; pointsMax: number; profileVersion: number }[];
}

export interface ApiError { status: number; error: string; message: string; details?: unknown; }

export function toApiError(e: unknown): ApiError {
  if (e instanceof HttpErrorResponse) {
    const body = e.error ?? {};
    if (e.status === 0) return { status: 0, error: 'network', message: 'The server could not be reached. Check your connection; your last saved answers are kept.' };
    return { status: e.status, error: body.error ?? 'http_' + e.status, message: body.message ?? e.message, details: body.details ?? body.report };
  }
  return { status: -1, error: 'unknown', message: String(e) };
}

@Injectable({ providedIn: 'root' })
export class Api {
  private http = inject(HttpClient);

  get<T>(url: string) { return firstValueFrom(this.http.get<T>(url)); }
  post<T>(url: string, body: unknown = {}) { return firstValueFrom(this.http.post<T>(url, body)); }
  put<T>(url: string, body: unknown) { return firstValueFrom(this.http.put<T>(url, body)); }
  delete<T>(url: string) { return firstValueFrom(this.http.delete<T>(url)); }

  catalog() { return this.get<CatalogItem[]>('/api/catalog'); }
  catalogDetail(code: string) { return this.get<CatalogDetail>(`/api/catalog/${encodeURIComponent(code)}`); }
  start(req: StartRequest) { return this.post<{ id: string }>('/api/attempts', req); }
  attempts(code?: string) { return this.get<AttemptSummary[]>('/api/attempts' + (code ? `?certificationCode=${encodeURIComponent(code)}` : '')); }
  state(id: string) { return this.get<AttemptState>(`/api/attempts/${id}`); }
  answer(id: string, pos: number, selected: string[]) { return this.put<{ position: number; selected: string[]; savedAt: string }>(`/api/attempts/${id}/items/${pos}/answer`, { selected }); }
  flag(id: string, pos: number, flagged: boolean) { return this.put(`/api/attempts/${id}/items/${pos}/flag`, { flagged }); }
  check(id: string, pos: number, revealWithoutAnswer = false) { return this.post<AttemptItem>(`/api/attempts/${id}/items/${pos}/check`, { revealWithoutAnswer }); }
  setFeedback(id: string, enabled: boolean) { return this.post<{ feedbackEnabled: boolean; classification: string }>(`/api/attempts/${id}/feedback`, { enabled }); }
  finish(id: string) { return this.post<AttemptSummary>(`/api/attempts/${id}/finish`); }
  result(id: string) { return this.get<AttemptResult>(`/api/attempts/${id}/result`); }
  progress(code: string) { return this.get<Progress>(`/api/progress?certificationCode=${encodeURIComponent(code)}`); }
}
