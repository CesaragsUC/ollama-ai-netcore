import { Injectable } from "@angular/core";
import { BaseService } from "./base.service";
import { HttpClient } from "@angular/common/http";
import { SendPromptDto } from "../models/chat.request";
import { catchError, map, Observable } from "rxjs";
import { AiResponse } from "../models/ai.response";


// services/ollama.api-service.ts
@Injectable({ providedIn: 'root' })
export class OllamaService extends BaseService {
  constructor(private http: HttpClient) { super(); }

  // default prompt sem stream
  sendPrompt(dto: SendPromptDto): Observable<AiResponse> {
    return this.http.post<AiResponse>(this.urlService + 'prompt', dto)
      .pipe(map(super.extractData), catchError(super.serviceError));
  }

  //default sem stream: prompt + múltiplos arquivos
  sendPromptWithFiles(dto: SendPromptDto, files: File[]): Observable<AiResponse> {
    const form = new FormData();
    form.append('dto', JSON.stringify(dto));

    for (const file of files) {
      form.append('files', file);
    }

    return this.http.post<AiResponse>(this.urlService + 'prompt-files', form)
      .pipe(map(super.extractData), catchError(super.serviceError));
  }

  streamPrompt(dto: SendPromptDto): Observable<string> {
    return new Observable<string>(observer => {
      const ac = new AbortController();

      fetch(this.urlService + 'prompt-stream', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(dto),
        signal: ac.signal
      })
        .then(async resp => {
          if (!resp.ok || !resp.body) throw new Error(`HTTP ${resp.status}`);
          const reader = resp.body!.getReader();
          const dec = new TextDecoder();

          let buf = '';
          for (; ;) {
            const { value, done } = await reader.read();
            if (done) break;

            buf += dec.decode(value, { stream: true });

            const events = buf.split(/\r?\n\r?\n/);
            buf = events.pop()!; // guarda possível parcial

            for (const evt of events) {
              for (const line of evt.split(/\r?\n/)) {
                const payload = this.sseData(line);
                if (payload != null && payload !== '[DONE]') {
                  observer.next(payload); 
                }
              }
            }
          }
          observer.complete();
        })
        .catch(err => observer.error(err));

      // cancelar ao unsubscribe
      return () => ac.abort();
    });
  }

  streamPromptWithFiles(dto: SendPromptDto, files: File[]): Observable<string> {
    return new Observable<string>(observer => {
      const form = new FormData();

      form.append('conversationId', (dto as any).ConversationId ?? (dto as any).conversationId ?? '');
      form.append('prompt', (dto as any).Prompt ?? (dto as any).prompt ?? '');
      for (const f of files) form.append('files', f, f.name);

      const ac = new AbortController();

      fetch(this.urlService + 'prompt-stream-files', {
        method: 'POST',
        body: form,
        signal: ac.signal
      })
        .then(async resp => {
          if (!resp.ok || !resp.body) throw new Error(`HTTP ${resp.status}`);
          const reader = resp.body!.getReader();
          const dec = new TextDecoder();

          let buf = '';
          for (; ;) {
            const { value, done } = await reader.read();
            if (done) break;

            buf += dec.decode(value, { stream: true });

            // eventos SSE são separados por linha em branco
            const events = buf.split(/\r?\n\r?\n/);
            buf = events.pop()!; // guarda possível parcial

            for (const evt of events) {
              for (const line of evt.split(/\r?\n/)) {
                const payload = this.sseData(line);
                if (payload != null && payload !== '[DONE]') {
                  observer.next(payload);
                }
              }
            }
          }
          observer.complete();
        })
        .catch(err => observer.error(err));

      return () => ac.abort();
    });
  }



  // helper: extrai o payload de uma linha SSE "data: ..."
  sseData(line: string) {
    if (!line.startsWith('data:')) return null;
    return line.startsWith('data: ')
      ? line.slice(6)   // remove "data: "
      : line.slice(5);  // remove "data:"
  }


}
