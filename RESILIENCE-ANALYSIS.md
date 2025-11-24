# Analiză Resiliență - Test Eșec Cassandra

## 📊 Comportament Actual (când un nod Cassandra eșuează)

### ✅ Ce funcționează:
1. **Operațiuni de citire (GET)** - Funcționează din cache
2. **Proxy și routing** - Funcționează normal
3. **Content negotiation** - Funcționează normal

### ❌ Ce nu funcționează:
1. **Operațiuni de scriere (POST/PUT/DELETE)** - Eșuează complet
2. **Operațiuni de citire noi (GET cache MISS)** - Eșuează dacă cache-ul expiră
3. **Nu există fallback mechanism** - Sistemul nu degradează grațios

### 🔍 Problema Root Cause:
- **Consistency Level: QUORUM** necesită 2 noduri (pentru replication_factor: 2)
- Când un nod eșuează, quorum nu poate fi atins
- Toate operațiunile care necesită quorum eșuează

---

## 🎯 Strategii de Îmbunătățire Resiliență

### Strategia 1: Adaptive Consistency Level (Recomandat)
**Descriere:** Folosește QUORUM când toate nodurile sunt disponibile, fallback la ONE când quorum nu este disponibil.

**Avantaje:**
- ✅ Maximizează consistența când este posibil
- ✅ Permite operațiuni când un nod eșuează
- ✅ Degradare grațioasă

**Dezavantaje:**
- ⚠️ Consistență redusă când folosește ONE
- ⚠️ Risc de date inconsistente (temporar)

### Strategia 2: Retry cu Exponential Backoff
**Descriere:** Reîncearcă operațiunile cu exponential backoff când eșuează.

**Avantaje:**
- ✅ Poate recupera din erori temporare
- ✅ Reduce impactul erorilor de rețea

**Dezavantaje:**
- ⚠️ Nu rezolvă problema fundamentală (quorum unavailable)
- ⚠️ Poate întârzia răspunsurile

### Strategia 3: Circuit Breaker Pattern
**Descriere:** Detectează eșecuri repetate și oprește temporar cererile către Cassandra.

**Avantaje:**
- ✅ Previne suprasolicitarea unui serviciu eșuat
- ✅ Răspunsuri rapide (fail-fast)

**Dezavantaje:**
- ⚠️ Necesită implementare suplimentară
- ⚠️ Poate bloca operațiuni valide

### Strategia 4: Graceful Degradation
**Descriere:** Permite operațiuni de citire din cache chiar dacă scrierea eșuează.

**Avantaje:**
- ✅ Sistemul rămâne parțial funcțional
- ✅ Utilizatorii pot continua să citească date

**Dezavantaje:**
- ⚠️ Datele pot deveni stale
- ⚠️ Utilizatorii nu pot crea/modifica date

---

## 💡 Recomandare: Implementare Hybrid

**Combină:**
1. **Adaptive Consistency Level** - Pentru operațiuni de scriere
2. **Error Handling îmbunătățit** - Pentru a returna erori clare
3. **Logging detaliat** - Pentru monitorizare
4. **Graceful Degradation** - Pentru operațiuni de citire

---

## 📝 Plan de Implementare

### Faza 1: Adaptive Consistency Level
- Detectează disponibilitatea nodurilor
- Folosește QUORUM când posibil, ONE când nu
- Loghează schimbările de consistency level

### Faza 2: Error Handling
- Tratează erorile Cassandra grațios
- Returnează răspunsuri HTTP clare (503 Service Unavailable)
- Loghează toate eșecurile

### Faza 3: Monitoring
- Adaugă metrici pentru disponibilitate Cassandra
- Alerte pentru eșecuri repetate
- Dashboard pentru status cluster

---

## ⚠️ Trade-offs

### Consistență vs Disponibilitate:
- **QUORUM:** Maximă consistență, disponibilitate redusă
- **ONE:** Disponibilitate maximă, consistență redusă
- **Adaptive:** Balanță între ambele

### Recomandare:
- Folosește **QUORUM** în mod normal (când toate nodurile sunt disponibile)
- Fallback la **ONE** doar când quorum nu este disponibil
- Loghează toate schimbările pentru audit

---

## 🔧 Implementare Tehnică

### Opțiunea 1: Consistency Level per Operation
- Operațiuni critice: QUORUM
- Operațiuni non-critice: ONE
- Fallback automat când quorum eșuează

### Opțiunea 2: Health Check Based
- Verifică statusul cluster-ului periodic
- Ajustează consistency level bazat pe health
- Cache-uiește statusul pentru performanță

### Opțiunea 3: Retry cu Fallback
- Încearcă cu QUORUM
- Dacă eșuează, reîncearcă cu ONE
- Loghează ambele încercări

---

## 📊 Metrici de Succes

### Disponibilitate:
- **Target:** 99.9% uptime chiar și cu un nod down
- **Măsurat:** % cereri reușite când un nod eșuează

### Performanță:
- **Target:** < 100ms pentru operațiuni de citire (din cache)
- **Target:** < 500ms pentru operațiuni de scriere (cu fallback)

### Consistență:
- **Target:** 100% consistență când toate nodurile sunt disponibile
- **Acceptabil:** Consistență eventuală când un nod eșuează

---

## ✅ Concluzie

**Comportament actual:** Sistemul eșuează complet când un nod Cassandra eșuează.

**Comportament dorit:** Sistemul degradează grațios, permitând operațiuni de citire și scriere cu consistență redusă.

**Recomandare:** Implementează Adaptive Consistency Level pentru a permite sistemului să funcționeze chiar și când un nod eșuează.

