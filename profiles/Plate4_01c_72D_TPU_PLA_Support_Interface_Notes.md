# Saved Settings: Plate 4 (`01c` Flat 72D TPU Cradle) with PLA Support Interface

Target setup: **Bambu Lab P2S** | **0.4 mm HF TC Nozzle** | **Elegoo TPU 72D (AMS)** + **PLA (AMS)**  
Ready-to-import process JSON: `0.20mm_Plate4_01c_Cradle_72D_TPU_PLA_SupportInterface_P2S_04_HF.json`

## 1. Support Material Assignment (Support Tab -> Filament for Supports)
- **Support/raft base:** `Default` *(Elegoo 72D TPU — prints 98% of support trunks with zero swaps)*
- **Support/raft interface:** `PLA` *(prints only the 2 non-stick wafer layers at the top of supports)*

## 2. Flushing Volumes & Prime Tower (Critical for 0.4 mm HF Nozzle!)
- **Flushing volumes -> `PLA → TPU`:** **`700 mm³`** *(manually override auto-calc to prevent PLA residue in the HF split-core from delaminating TPU layers)*
- **Prime Tower:** **`ON`** (`Width = 35 mm`, `Prime volume = 45 mm³`)
- **Flush into objects' infill:** **`OFF`**
- **Flush into objects' support:** **`OFF`**

## 3. Support Geometry & Speed
- **Type / Style:** `Tree (auto)` -> `Tree Slim`
- **On build plate only:** **`ON`**
- **Top Z distance:** **`0.0 mm`**
- **Top interface layers:** **`2`**
- **Top interface pattern:** **`Rectilinear`**
- **Top interface spacing:** **`0.0 mm`** *(solid wafer peels out in one piece)*
- **Support/object XY distance:** **`0.35 mm`**
- **Independent support layer height:** **`OFF`**
- **Support interface speed:** **`45 mm/s`** *(gentle laydown on top of TPU support trunks)*

## 4. Temps & Enclosure
- **72D TPU Nozzle:** `230 °C` (1st) / `235 °C` (other)
- **PLA Interface Nozzle:** `220 °C` *(tight 15 °C gap prevents PLA cooking or slow swaps)*
- **Bed Temp:** `45 °C` on PEI with **thin PVA glue stick release coat**
- **Chamber:** Vent top glass / crack door (`< 35 °C`) so PLA doesn't heat-creep between swaps

## 5. Post-Print Checklist
- Pop out the 2-layer PLA wafers and TPU support trunks.
- Flex the retention clip & nose cup firmly by hand at the swap Z-heights to verify zero delamination.
- Snap the **Plate 5 PETG 6-Nut Carrier Plate (`05a`)** into the belly pocket.
