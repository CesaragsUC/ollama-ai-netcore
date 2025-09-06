import {
  AfterViewInit, Component, ElementRef, Inject, OnInit, PLATFORM_ID,
  ViewChild, ViewChildren, QueryList
} from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { OllamaService } from '../services/ollama.api-service';
import { SendPromptDto } from '../models/chat.request';
import { Observable, Subscription } from 'rxjs';

type Role = 'user' | 'ai';
interface ChatMessage { id: number; role: Role; content: string; animate?: boolean; }

@Component({
  selector: 'app-new-chat',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: 'new-chat.component.html',
  styleUrls: ['new-chat.component.css'],
  providers: [OllamaService]
})
export class NewChatComponent implements OnInit, AfterViewInit {
  @ViewChild('chatMessages', { static: false }) chatMessages!: ElementRef<HTMLDivElement>;

  promptMessage = '';
  selectedFiles: File[] = [];   // NOVO
  private idSeq = 1;
  private streamSub?: Subscription;
  welcomeText = "Hello! I'm your AI assistant. How can I help you today?";
  messages: ChatMessage[] = [];
  private isBrowser = false;
  
  constructor(
    private chatService: OllamaService,
    @Inject(PLATFORM_ID) private platformId: Object
  ) 
  {
    this.isBrowser = isPlatformBrowser(this.platformId);
  }

  promptDto: SendPromptDto = { Prompt: '', ConversationId: '' };

  ngOnInit(): void { }
  
  ngAfterViewInit(): void {
     this.showWelcomeTyping();
  }

  onFilesSelected(evt: Event) {
    const input = evt.target as HTMLInputElement;
    if (!input.files || !input.files.length) return;

    this.selectedFiles = [...this.selectedFiles, ...Array.from(input.files)];
    input.value = '';
  }

  removeFile(index: number) {
    this.selectedFiles = this.selectedFiles.filter((_, i) => i !== index);
  }

  // versão com stream
  sendStreamPrompt(event?: Event) {
    // Enter sem Shift envia
    if (event instanceof KeyboardEvent && !event.shiftKey) {
      event.preventDefault();
    }

    const text = this.promptMessage.trim();
    if (!text && !this.selectedFiles.length) return;

    this.promptDto.Prompt = text;

    const userLabel = text || (this.selectedFiles.length ? '(files attached)' : '');
    this.messages.push({ id: this.idSeq++, role: 'user', content: userLabel });
    this.scrollBottom?.();

    const aiMsg: ChatMessage = { id: this.idSeq++, role: 'ai', content: '', animate: true };
    this.messages.push(aiMsg);
    this.scrollBottom?.();

     // cancela stream anterior, se houver
    this.streamSub?.unsubscribe();
    this.promptMessage = ''; // limpa imput depois que envia a msg
     
    const hasFiles = this.selectedFiles.length > 0;

    let src$: Observable<string>;
    if (hasFiles) {
      src$ = this.chatService.streamPromptWithFiles(this.promptDto, this.selectedFiles);
    } else {
      src$ = this.chatService.streamPrompt(this.promptDto);
    }

   let acc = '';

    this.streamSub = src$.subscribe({
      next: (chunk: string) => {
        acc += chunk;               // acumula pedaços
        aiMsg.content = acc;      
        this.messages = [...this.messages]; 
        this.scrollBottom?.();
      },
      complete: () => {
        aiMsg.animate = false;      // para o cursor
        this.messages = [...this.messages];
        // limpa input e anexos
        this.selectedFiles = [];
        this.promptMessage = '';
        this.stopAllCursors();
      },
      error: (err) => {
        aiMsg.content = 'Erro ao receber stream.';
        aiMsg.animate = false;
        this.messages = [...this.messages];
        console.error(err);
      }
    });

  }


 sendPrompt(event?: Event) {
    // Enter sem Shift envia
    if (event instanceof KeyboardEvent && !event.shiftKey) {
      event.preventDefault();
    }

    const text = this.promptMessage.trim();
    if (!text && !this.selectedFiles.length) return;

    this.promptDto.Prompt = text;
    const userLabel = text || (this.selectedFiles.length ? '(files attached)' : '');
    this.messages.push({ id: this.idSeq++, role: 'user', content: userLabel });
    this.scrollBottom?.();

    const aiMsg: ChatMessage = { id: this.idSeq++, role: 'ai', content: '', animate: true };
    this.messages.push(aiMsg);
    this.scrollBottom?.();

    const hasFiles = this.selectedFiles.length > 0;

    const obs = hasFiles
      ? this.chatService.sendPromptWithFiles(this.promptDto, this.selectedFiles)
      : this.chatService.sendPrompt(this.promptDto);

    obs.subscribe({
      next: (res: any) => {
        const answer: string = res?.message ?? 'Ok!';
        aiMsg.content = answer;
        this.typeWriter(aiMsg, answer); 
        this.selectedFiles = [];
        this.promptMessage = '';
      },
      error: (err) => {
        aiMsg.content = 'Sorry, something went wrong.';
        aiMsg.animate = false;
        this.messages = [...this.messages];
        console.error(err);
      }
    });
  }


  // === helpers ===
  trackById = (_: number, m: ChatMessage) => m.id;


  private typeWriter(msg: ChatMessage, full: string, cps = 20) {
    msg.content = '';
    msg.animate = true;

    const chars = Array.from(full);
    let i = 0;

    const tick = () => {
      const step = 3;
      msg.content += chars.slice(i, i + step).join('');
      i += step;

      this.messages = [...this.messages];  
      this.scrollBottom?.();

      if (i < chars.length) {
        window.setTimeout(tick, Math.max(15, Math.floor(500 / cps)));
      } else {
        msg.animate = false;
        this.messages = [...this.messages];
      }
    };
    tick();
  }

  private stopAllCursors() {
    let changed = false;
    for (const m of this.messages) {
      if (m.role === 'ai' && m.animate) {
        m.animate = false;
        changed = true;
      }
    }
    if (changed) this.messages = [...this.messages]; // força CD
  }

  private showWelcomeTyping() {
    // se quiser garantir que nenhum cursor ficou ligado
    this.stopAllCursors?.();

    const m: ChatMessage = { id: this.idSeq++, role: 'ai', content: '', animate: true };
    this.messages = [m];                // ou this.messages.push(m) se já houver histórico
    this.messages = [...this.messages]; // força CD

    this.typeWriter(m, this.welcomeText, 28); // cps=28, 3 chars por tick
  }
  
  private scrollBottom() {
    const box = this.chatMessages?.nativeElement;
    if (box) box.scrollTop = box.scrollHeight;
  }
}
