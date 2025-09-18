#  ✨ Ollama AI PoC — .NET 9 (API) + Angular v18+ 

![screem demo](https://github.com/user-attachments/assets/b2ba1a17-0a78-45e7-ae9d-d48928596bd5)



## PoC for local AI chat using Ollama  
with two models:

**phi3** → used for text/files (e.g.: PDF → text).  
https://ollama.com/library/phi3  

**llama3.2-vision** → used for images (multimodal).  
https://ollama.com/library/llama3.2-vision  

The frontend consumes responses via streaming (.NET API) to display the output token by token with a typing effect.  

🛠️ Configuration:
```
#1 Install Ollama
Here: https://ollama.com/

#2 Download the models
ollama pull phi3:3.8b
ollama pull llama3.2-vision:11b-instruct-q4_K_M
```
### 🔍 How to check if Ollama is running:
- http://localhost:11434/
<img width="375" height="164" alt="image" src="https://github.com/user-attachments/assets/d90607ff-0331-4a99-a64d-e564a06648dc" />

### 📌 llama3.2-vision model parameters:

- **`q4_K_M`**  -> Type of quantization of the model (GGUF format from llama.cpp).
- **`11b`** -> ~11 billion parameters
- **`instruct`**  → fine-tuned to follow instructions
- **`q4_K_M`**  → K-quant 4-bit, “M” variant (medium)

### 🔎 What this means in practice:

- q4 averages ~4 bits per weight (highly compressed).
- Less RAM/VRAM usage and faster generation.
- Slightly lower quality than less aggressive quantizations (q5/q6/q8).
- K → “K-quants”, a newer family of quantizations in llama.cpp (better quality×memory tradeoff than the older q4_0/q4_1).
- M → “Medium”: balanced profile between quality and memory usage.
- q4_K_S (Small) uses a little less memory, loses more quality.
- q4_K_M is the recommended balance in 4-bit.
- If you want more fidelity (and have memory), use q5_K_M / q6_K / q8_0.

### 🔧 How to use other quantizations in Ollama:
````
# download another quantization
ollama pull llama3.2-vision:11b-instruct-q5_K_M

# run specifying the desired tag
ollama run llama3.2-vision:11b-instruct-q5_K_M
````

## Architecture:

```
Angular (UI + API)
  ├── POST /api/chat/send-stream          → text  (uses phi3)
  └── POST /api/chat/send-stream-files    → files/images
                                           • PDF → text → phi3
                                           • image/* → llama3.2-vision

Ollama (localhost:11434)
  ├── phi3:3.8b
  └── llama3.2-vision:11b-instruct-q4_K_M
```

## Useful commands (Ollama)

````
# list installed models
ollama list

# model details
ollama show llama3.2-vision:11b-instruct-q4_K_M

# run a model manually (quick test)
ollama run phi3:3.8b

# remove a model
ollama rm phi3:3.8b
````

## Backend Project
````
dotnet restore
dotnet run
````

## Front Project
````
npm install
ng serve -o
````
