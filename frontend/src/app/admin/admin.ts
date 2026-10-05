import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Api, toApiError } from '../core/api';

type Tab = 'questions' | 'coverage' | 'imports' | 'sources' | 'exam' | 'audit';

interface QRow { questionId: string; externalId: string; version: number; status: string; domainCode: string; objective: string; questionType: string; locale: string; stem: string; versions: number; }
interface ImportReport {
  fileName: string; valid: boolean; fileErrors: string[]; casesNew: number; casesUnchanged: number; toCreate: number; unchanged: number; toVersion: number; rejected: number;
  items: { externalId: string; location?: string; action: string; issues: { severity: string; code: string; message: string }[] }[];
}

@Component({
  selector: 'app-admin',
  imports: [RouterLink, DatePipe],
  templateUrl: './admin.html',
  styleUrl: './admin.css',
})
export class AdminPage implements OnInit {
  private api = inject(Api);
  private http = inject(HttpClient);
  protected tab = signal<Tab>('questions');
  protected certs = signal<{ code: string; title: string }[]>([]);
  protected cert = signal('CCDV-F');
  protected status = signal('');
  protected domain = signal('');
  protected rows = signal<QRow[]>([]);
  protected coverage = signal<any>(null);
  protected stats = signal<any>(null);
  protected sources = signal<any[]>([]);
  protected certDetail = signal<any>(null);
  protected audit = signal<any[]>([]);
  protected imports = signal<any[]>([]);
  protected report = signal<ImportReport | null>(null);
  protected file = signal<File | null>(null);
  protected message = signal<string | null>(null);
  protected error = signal<string | null>(null);
  protected busy = signal(false);
  protected lang = signal({ locale: '', sourceId: '', evidence: '' });
  readonly statuses = ['draft', 'technical_review', 'editorial_review', 'approved', 'published', 'quarantined', 'retired'];

  async ngOnInit() {
    const c = await this.api.catalog();
    this.certs.set(c.map((x) => ({ code: x.code, title: x.title })));
    await this.refresh();
  }

  async show(t: Tab) { this.tab.set(t); this.message.set(null); this.error.set(null); await this.refresh(); }
  async setCert(c: string) { this.cert.set(c); this.domain.set(''); await this.refresh(); }

  async refresh() {
    const c = encodeURIComponent(this.cert());
    try {
      switch (this.tab()) {
        case 'questions': {
          let url = `/api/admin/questions?certificationCode=${c}`;
          if (this.status()) url += `&status=${this.status()}`;
          if (this.domain()) url += `&domain=${this.domain()}`;
          this.rows.set(await this.api.get<QRow[]>(url));
          if (!this.coverage()) this.coverage.set(await this.api.get(`/api/admin/coverage?certificationCode=${c}`));
          break;
        }
        case 'coverage':
          this.coverage.set(await this.api.get(`/api/admin/coverage?certificationCode=${c}`));
          this.stats.set(await this.api.get(`/api/admin/content-stats?certificationCode=${c}`));
          break;
        case 'imports': this.imports.set(await this.api.get<any[]>('/api/admin/imports')); break;
        case 'sources': this.sources.set(await this.api.get<any[]>('/api/admin/sources')); break;
        case 'exam': this.certDetail.set(await this.api.get(`/api/admin/certifications/${c}`)); this.sources.set(await this.api.get<any[]>('/api/admin/sources')); break;
        case 'audit': this.audit.set(await this.api.get<any[]>('/api/admin/audit')); break;
      }
    } catch (e) { this.error.set(toApiError(e).message); }
  }

  domainsOf() { return (this.coverage()?.domains ?? []) as { code: string; name: string }[]; }

  pick(e: Event) { this.file.set((e.target as HTMLInputElement).files?.[0] ?? null); this.report.set(null); this.message.set(null); }

  async upload(kind: 'preview' | 'commit') {
    const f = this.file();
    if (!f) return;
    this.busy.set(true); this.error.set(null); this.message.set(null);
    const form = new FormData();
    form.append('file', f, f.name);
    try {
      const r = await firstValueFrom(this.http.post<any>(`/api/admin/imports/${kind}`, form));
      if (kind === 'preview') this.report.set(r);
      else { this.report.set(r.report); this.message.set(`Imported. ${r.report.toCreate} new, ${r.report.toVersion} new versions, ${r.report.unchanged} unchanged. New content starts as draft.`); await this.refresh(); }
    } catch (e) {
      const a = toApiError(e);
      if (a.details && typeof a.details === 'object') this.report.set(a.details as ImportReport);
      this.error.set(a.message);
    } finally { this.busy.set(false); }
  }

  async verifyLanguage() {
    try {
      await this.api.post(`/api/admin/certifications/${encodeURIComponent(this.cert())}/languages`, this.lang());
      this.message.set('Language verified.'); this.lang.set({ locale: '', sourceId: '', evidence: '' });
      await this.refresh();
    } catch (e) { this.error.set(toApiError(e).message); }
  }

  async removeLanguage(locale: string) {
    try { await this.api.delete(`/api/admin/certifications/${encodeURIComponent(this.cert())}/languages/${locale}`); await this.refresh(); }
    catch (e) { this.error.set(toApiError(e).message); }
  }

  protected profileDraft = signal('');
  draftFromCurrent() {
    const cur = this.certDetail()?.profiles?.find((p: any) => p.isCurrent);
    if (!cur) return;
    const { questionCount, examDurationMinutes, appointmentDurationMinutes, allowedQuestionTypes, domainWeights, simulatorPassPercent, verificationStatus, sourceIds, blueprintVersion, notes } = cur;
    this.profileDraft.set(JSON.stringify({ questionCount, examDurationMinutes, appointmentDurationMinutes, allowedQuestionTypes, domainWeights, simulatorPassPercent, verificationStatus, sourceIds, blueprintVersion, notes }, null, 2));
  }
  async saveProfile() {
    let body: unknown;
    try { body = JSON.parse(this.profileDraft()); } catch { this.error.set('The profile is not valid JSON.'); return; }
    try {
      await this.api.post(`/api/admin/certifications/${encodeURIComponent(this.cert())}/profiles`, body);
      this.message.set('New profile version saved. Attempts already started keep the version they used.');
      this.profileDraft.set(''); await this.refresh();
    } catch (e) { this.error.set(toApiError(e).message); }
  }

  setLang(k: 'locale' | 'sourceId' | 'evidence', v: string) { this.lang.update((l) => ({ ...l, [k]: v })); }
  statusLabel(s: string) { return s.replaceAll('_', ' '); }
  objectKeys(o: object | null | undefined) { return o ? Object.keys(o) : []; }
}
