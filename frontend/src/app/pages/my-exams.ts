import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Api, AttemptSummary, CatalogItem, toApiError } from '../core/api';
import { Auth } from '../core/auth';

@Component({
  selector: 'app-my-exams',
  imports: [RouterLink, DatePipe, DecimalPipe],
  template: `
    <div class="page">
      <h1>My exams</h1>
      @if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
      @if (inProgress().length) {
        <h2>In progress</h2>
        <ul class="list">
          @for (a of inProgress(); track a.id) {
            <li class="sheet row">
              <div><strong>{{ a.certificationCode }}</strong> {{ modeName(a.mode) }} <span class="muted small">started {{ a.startedAt | date: 'medium' }}</span>
                @if (a.deadlineAt) { <span class="small"> Ends {{ a.deadlineAt | date: 'shortTime' }}.</span> }</div>
              <a class="btn" [routerLink]="['/attempts', a.id]">Continue</a>
            </li>
          }
        </ul>
      }
      <h2>Progress by exam</h2>
      <p>@for (c of catalog(); track c.code) { <a class="btn secondary" [routerLink]="['/progress', c.code]">{{ c.code }} progress</a> }</p>

      <h2>History</h2>
      @if (!loading() && done().length === 0) { <p>No finished attempts yet. <a routerLink="/exams">Pick an exam</a> to start.</p> }
      @if (done().length) {
        <div class="table-scroll">
          <table class="data">
            <thead><tr><th scope="col">Finished</th><th scope="col">Exam</th><th scope="col">Mode</th><th scope="col">Type</th><th scope="col">Score</th><th scope="col">Profile</th><th scope="col"><span class="visually-hidden">Actions</span></th></tr></thead>
            <tbody>
              @for (a of done(); track a.id) {
                <tr>
                  <td>{{ a.finishedAt | date: 'medium' }}</td>
                  <td>{{ a.certificationCode }}</td>
                  <td>{{ modeName(a.mode) }}{{ a.preview ? ' (preview)' : '' }}</td>
                  <td><span class="tag" [class.ok]="a.classification === 'clean'" [class.warn]="a.classification === 'assisted'">{{ a.classification }}</span></td>
                  <td>{{ a.score?.pointsEarned }}/{{ a.score?.pointsMax }} ({{ a.score?.percent | number: '1.0-1' }}%){{ a.status === 'expired' ? ', time out' : '' }}</td>
                  <td>v{{ a.profileVersion }}</td>
                  <td><a [routerLink]="['/attempts', a.id, 'result']">Review</a></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }

      <section class="danger-zone" aria-labelledby="dz">
        <h2 id="dz">Your data</h2>
        <p class="small">Deleting your account removes your attempts, answers and history. This cannot be undone.</p>
        @if (confirmDelete()) {
          <p><button class="btn danger" type="button" (click)="deleteAccount()">Delete my account and data</button>
             <button class="btn quiet" type="button" (click)="confirmDelete.set(false)">Cancel</button></p>
        } @else { <button class="btn danger" type="button" (click)="confirmDelete.set(true)">Delete account…</button> }
      </section>
    </div>
  `,
  styles: `
    .list { list-style: none; padding: 0; display: grid; gap: 0.5rem; }
    .row { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap; padding: 0.75rem 1rem; }
    .danger-zone { margin-top: 3rem; border-top: 1px solid var(--rule); padding-top: 1rem; }
    p .btn { margin-right: 0.5rem; }
  `,
})
export class MyExamsPage implements OnInit {
  private api = inject(Api);
  private auth = inject(Auth);
  private router = inject(Router);
  protected all = signal<AttemptSummary[]>([]);
  protected catalog = signal<CatalogItem[]>([]);
  protected loading = signal(true);
  protected error = signal<string | null>(null);
  protected confirmDelete = signal(false);
  protected inProgress = computed(() => this.all().filter((a) => a.status === 'in_progress'));
  protected done = computed(() => this.all().filter((a) => a.status !== 'in_progress'));

  async ngOnInit() {
    try {
      const [a, c] = await Promise.all([this.api.attempts(), this.api.catalog()]);
      this.all.set(a); this.catalog.set(c);
    } catch (e) { this.error.set(toApiError(e).message); }
    finally { this.loading.set(false); }
  }
  modeName(m: string) { return m === 'simulation' ? 'Simulation' : m === 'custom' ? 'Custom set' : 'Practice'; }
  async deleteAccount() {
    try { await this.auth.deleteAccount(); this.router.navigateByUrl('/'); }
    catch (e) { this.error.set(toApiError(e).message); }
  }
}
