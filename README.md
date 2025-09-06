#  ✨ Ollama AI PoC — .NET 9 (API) + Angular (Web)

## PoC de chat com IA local usando Ollama
 com dois modelos:

**phi3** → usado para texto/arquivos (ex.: PDF → texto).
https://ollama.com/library/phi3

**llama3.2-vision** → usado para imagens (multimodal).
https://ollama.com/library/llama3.2-vision

O frontend consome respostas via streaming (.NET API) para mostrar a saída token a token com efeito de digitação.

🛠️ Configuration:
```

#1 Instale o Ollama
Aqui: https://ollama.com/

#2 Baixe os modelos
ollama pull phi3:3.8b
ollama pull llama3.2-vision:11b-instruct-q4_K_M
```
### 🔍 Como se Ollama esta rodando:
- http://localhost:11434/
<img width="375" height="164" alt="image" src="https://github.com/user-attachments/assets/d90607ff-0331-4a99-a64d-e564a06648dc" />

### 📌 Parametros do modelo llama3.2-vision:

- **`q4_K_M`**  -> É o tipo de quantização do modelo (formato GGUF do llama.cpp).
- **`11b`** -> ~11 bilhões de parâmetros
- **`instruct`**  → afinado para seguir instruções
- **`q4_K_M `**  → K-quant 4-bit, variante “M” (medium)

### 🔎 O que isso significa na prática:

- q4 em média ~4 bits por peso (bem comprimido).
- Menos RAM/VRAM usada e geração mais rápida.
- Qualidade um pouco abaixo de quantizações menos agressivas (q5/q6/q8).
- K → “K-quants”, uma família de quantizações mais modernas do llama.cpp (melhor relação qualidade×memória que as antigas q4_0/q4_1).
- M → “Medium”: perfil equilibrado de qualidade vs. uso de memória.
- q4_K_S (Small) usa um pouco menos de memória, perde mais qualidade.
- q4_K_M é o equilíbrio recomendado em 4-bit.
- Se quiser mais fidelidade (e tiver memória), use q5_K_M / q6_K / q8_0.

### 🔧 Como usar outras quantizações no Ollama:
````
# baixar outra quantização
ollama pull llama3.2-vision:11b-instruct-q5_K_M

# rodar especificando a tag desejada
ollama run llama3.2-vision:11b-instruct-q5_K_M

````

## Arquitetura:

```
Angular (UI + API)
  ├── POST /api/chat/send-stream          → texto  (usa phi3)
  └── POST /api/chat/send-stream-files    → arquivos/imagens
                                           • PDF → texto → phi3
                                           • image/* → llama3.2-vision

Ollama (localhost:11434)
  ├── phi3:3.8b
  └── llama3.2-vision:11b-instruct-q4_K_M
```

## Comandos úteis (Ollama)

````
# listar modelos instalados
ollama list

# detalhes de um modelo
ollama show llama3.2-vision:11b-instruct-q4_K_M

# rodar um modelo manualmente (teste rápido)
ollama run phi3:3.8b

# remover um modelo
ollama rm phi3:3.8b

````
