#!/usr/bin/env python3
"""Apply the Horizon-specific runtime-loading fixes to the pinned upstream ASI source."""
from pathlib import Path
import sys

if len(sys.argv) != 2:
    raise SystemExit("Uso: patch_upstream.py <carpeta-upstream>")

root = Path(sys.argv[1])
main_path = root / "client/src/main.cpp"
loader_path = root / "client/src/model_loader.cpp"

def read(path: Path) -> str:
    if not path.is_file():
        raise SystemExit(f"No existe el archivo esperado: {path}")
    return path.read_text(encoding="utf-8")

def write(path: Path, content: str) -> None:
    path.write_text(content, encoding="utf-8", newline="\n")

def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"Parche detenido: {label}; se esperaba 1 coincidencia y hay {count}.")
    return text.replace(old, new, 1)

# 1) Nunca descargar desde DllMain (loader lock de Windows).
main = read(main_path)
main = replace_once(
    main,
    'static DWORD WINAPI InitThread(LPVOID) {\n',
    'static DWORD WINAPI InitThread(LPVOID) {\n'
    '    // Esperar a que Windows termine DllMain antes de usar WinINet.\n'
    '    Sleep(1000);\n'
    '    ModelLoader::earlyDownload();\n',
    "mover descarga al hilo de inicializacion"
)
main = replace_once(
    main,
    '        ModelLoader::earlyDownload(); // descarga DFFs/TXDs faltantes\n',
    '',
    "quitar descarga de DllMain"
)
main = replace_once(
    main,
    '        ModelLoader::earlyPatch();    // parchea gta3.img antes de que GTA SA lo abra\n',
    '',
    "evitar parcheo incompleto de gta3.img"
)
write(main_path, main)

loader = read(loader_path)

# 2) Descargar a .part, validar HTTP 2xx y promover el archivo solo si termino bien.
download_start = loader.find("static bool downloadFile(")
download_end_marker = "\n}\n\n// Estructura con toda la info de una entrada del ini"
download_end = loader.find(download_end_marker, download_start)
if download_start < 0 or download_end < 0:
    raise SystemExit("No se encontro el bloque downloadFile esperado.")
download_end += 2
download_replacement = StringRaw = r'''static bool downloadFile(const std::string& url, const std::string& localPath) {
    logMsg("[cv_client] Descargando: %s", url.c_str());

    HINTERNET hNet = InternetOpenA("custom_vehicles_asi/1.0",
                                    INTERNET_OPEN_TYPE_PRECONFIG, nullptr, nullptr, 0);
    if (!hNet) { logMsg("[cv_client] InternetOpen fallo"); return false; }

    HINTERNET hUrl = InternetOpenUrlA(hNet, url.c_str(), nullptr, 0,
                                       INTERNET_FLAG_RELOAD | INTERNET_FLAG_NO_CACHE_WRITE |
                                       INTERNET_FLAG_PRAGMA_NOCACHE, 0);
    if (!hUrl) {
        logMsg("[cv_client] No se pudo abrir URL: %s (error %lu)", url.c_str(), GetLastError());
        InternetCloseHandle(hNet);
        return false;
    }

    DWORD status = 0;
    DWORD statusSize = sizeof(status);
    if (!HttpQueryInfoA(hUrl, HTTP_QUERY_STATUS_CODE | HTTP_QUERY_FLAG_NUMBER,
                        &status, &statusSize, nullptr) || status < 200 || status >= 300) {
        logMsg("[cv_client] HTTP no exitoso para %s (status=%lu)", url.c_str(), status);
        InternetCloseHandle(hUrl);
        InternetCloseHandle(hNet);
        return false;
    }

    createDirectories(localPath);
    const std::string tempPath = localPath + ".part";
    HANDLE hFile = CreateFileA(tempPath.c_str(), GENERIC_WRITE, 0, nullptr,
                                CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (hFile == INVALID_HANDLE_VALUE) {
        logMsg("[cv_client] No se pudo crear: %s", tempPath.c_str());
        InternetCloseHandle(hUrl);
        InternetCloseHandle(hNet);
        return false;
    }

    bool ok = true;
    DWORD total = 0;
    char buf[8192];
    for (;;) {
        DWORD bytesRead = 0;
        if (!InternetReadFile(hUrl, buf, sizeof(buf), &bytesRead)) {
            ok = false;
            break;
        }
        if (bytesRead == 0) break;

        DWORD bytesWritten = 0;
        if (!WriteFile(hFile, buf, bytesRead, &bytesWritten, nullptr) ||
            bytesWritten != bytesRead) {
            ok = false;
            break;
        }
        total += bytesWritten;
    }

    CloseHandle(hFile);
    InternetCloseHandle(hUrl);
    InternetCloseHandle(hNet);

    if (!ok || total == 0) {
        DeleteFileA(tempPath.c_str());
        logMsg("[cv_client] Descarga incompleta: %s", url.c_str());
        return false;
    }

    if (!MoveFileExA(tempPath.c_str(), localPath.c_str(),
                     MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH)) {
        DeleteFileA(tempPath.c_str());
        logMsg("[cv_client] No se pudo finalizar el archivo: %s", localPath.c_str());
        return false;
    }

    logMsg("[cv_client] Descargado: %s (%lu bytes)", localPath.c_str(), total);
    return true;
}'''
loader = loader[:download_start] + download_replacement + loader[download_end:]

# 3) Desactivar por completo el parcheo manual de gta3.img.
early_start = loader.find("void ModelLoader::earlyPatch() {")
early_end_marker = "\n}\n\n// FASE 2: despues del streaming"
early_end = loader.find(early_end_marker, early_start)
if early_start < 0 or early_end < 0:
    raise SystemExit("No se encontro el bloque earlyPatch esperado.")
early_end += 2
early_replacement = '''void ModelLoader::earlyPatch() {
    // Desactivado para Horizon: el modelo se carga directamente en memoria
    // desde DFF/TXD mediante registerModel(). No modificar gta3.img del jugador.
    logMsg("[cv_client] Parcheo de gta3.img desactivado; carga directa en memoria");
}'''
loader = loader[:early_start] + early_replacement + loader[early_end:]

# 4) init() debe consumir custom_vehicles.ini y registrar cada modelo. Antes
# solo guardaba offsets para un segundo arranque y nunca llamaba a loadConfig().
init_start = loader.find("void ModelLoader::init() {")
init_end_marker = "\n}\n\nvoid ModelLoader::applyPatches()"
init_end = loader.find(init_end_marker, init_start)
if init_start < 0 or init_end < 0:
    raise SystemExit("No se encontro el bloque ModelLoader::init esperado.")
init_end += 2
init_replacement = '''void ModelLoader::init() {
    logMsg("[cv_client] Inicializando ModelLoader");
    loadConfig();
    logMsg("[cv_client] Registro de modelos de Horizon finalizado");
}'''
loader = loader[:init_start] + init_replacement + loader[init_end:]

# 5) Reemplazar registerModel por una variante que nunca libera el model-info
# original de GTA si falla la carga de la textura o del DFF.
reg_start = loader.find("bool ModelLoader::registerModel(")
reg_end = loader.find("\n// ============================================================", reg_start)
if reg_start < 0 or reg_end < 0:
    raise SystemExit("No se encontro el bloque registerModel esperado.")
reg_replacement = r'''bool ModelLoader::registerModel(int modelId, const std::string& dffPath, const std::string& txdPath) {
    const bool isExistingSA = (modelId >= 400 && modelId <= 611);
    const bool isCustomId = (modelId >= CUSTOM_ID_MIN && modelId <= CUSTOM_ID_MAX);
    if (!isExistingSA && !isCustomId) {
        logMsg("[cv_client] ID %d fuera de rango valido", modelId);
        return false;
    }
    if (modelId >= gta::MODEL_TABLE_SIZE) {
        logMsg("[cv_client] ID %d excede la tabla de GTA SA (%d)", modelId, gta::MODEL_TABLE_SIZE);
        return false;
    }

    void** table = gta::modelInfoTable();
    const std::string gameDir = getGameDir();
    const std::string absDff = gameDir + "\\" + dffPath;
    const std::string absTxd = gameDir + "\\" + txdPath;

    if (GetFileAttributesA(absDff.c_str()) == INVALID_FILE_ATTRIBUTES) {
        logMsg("[cv_client] DFF no encontrado: %s", absDff.c_str());
        return false;
    }
    if (GetFileAttributesA(absTxd.c_str()) == INVALID_FILE_ATTRIBUTES) {
        logMsg("[cv_client] TXD no encontrado: %s", absTxd.c_str());
        return false;
    }

    void* existingInfo = table[modelId];
    void* newInfo = nullptr;
    int16_t originalTxdIndex = -1;
    gta::RwObject* originalRwObject = nullptr;

    if (isExistingSA) {
        if (!existingInfo) {
            logMsg("[cv_client] Model info original %d no disponible", modelId);
            return false;
        }
        // Reutilizar el model-info original conserva tipo, handling visual,
        // damage frames y la estructura nativa del vehiculo.
        newInfo = existingInfo;
        originalTxdIndex = gta::modelField<int16_t>(newInfo, gta::OFF_TXD_INDEX);
        originalRwObject = gta::modelField<gta::RwObject*>(newInfo, gta::OFF_RW_OBJECT);
    } else {
        void* baseInfo = table[BASE_VEHICLE_ID];
        if (!baseInfo) {
            logMsg("[cv_client] Model info base (%d) no disponible", BASE_VEHICLE_ID);
            return false;
        }
        newInfo = std::malloc(gta::SIZEOF_VEHICLE_MODEL_INFO);
        if (!newInfo) {
            logMsg("[cv_client] malloc fallo para modelo %d", modelId);
            return false;
        }
        std::memcpy(newInfo, baseInfo, gta::SIZEOF_VEHICLE_MODEL_INFO);
    }

    auto rollbackModelInfo = [&]() {
        if (isExistingSA) {
            table[modelId] = existingInfo;
            gta::modelField<int16_t>(existingInfo, gta::OFF_TXD_INDEX) = originalTxdIndex;
            gta::modelField<gta::RwObject*>(existingInfo, gta::OFF_RW_OBJECT) = originalRwObject;
        } else {
            if (table[modelId] == newInfo) table[modelId] = nullptr;
            std::free(newInfo);
        }
    };

    char txdName[32];
    std::snprintf(txdName, sizeof(txdName), "cv_%d", modelId);
    const int txdSlot = gta::AddTxdSlot(txdName);
    if (txdSlot < 0) {
        logMsg("[cv_client] AddTxdSlot fallo para modelo %d", modelId);
        rollbackModelInfo();
        return false;
    }

    DWORD txdExc = 0;
    if (!tryLoadTxd(txdSlot, absTxd.c_str(), &txdExc)) {
        logMsg("[cv_client] LoadTxd fallo para modelo %d (exception=0x%08X)",
               modelId, txdExc);
        gta::RemoveTxdSlot(txdSlot);
        rollbackModelInfo();
        return false;
    }

    gta::SetCurrentTxd(txdSlot);
    gta::modelField<int16_t>(newInfo, gta::OFF_TXD_INDEX) = static_cast<int16_t>(txdSlot);
    table[modelId] = newInfo;

    applyNullCheckPatches();
    void* previousCurrentInfo = *reinterpret_cast<void**>(0x9689E0);
    *reinterpret_cast<void**>(0x9689E0) = newInfo;

    gta::RwStream* stream = gta::RwStreamOpen(
        gta::rwSTREAMFILENAME, gta::rwSTREAMREAD, absDff.c_str());
    if (!stream) {
        *reinterpret_cast<void**>(0x9689E0) = previousCurrentInfo;
        logMsg("[cv_client] RwStreamOpen fallo: %s", absDff.c_str());
        gta::RemoveTxdSlot(txdSlot);
        rollbackModelInfo();
        return false;
    }

    bool loadOk = false;
    gta::RpClump* clump = nullptr;
    DWORD clumpExc = 0;

    if (isExistingSA) {
        logMsg("[cv_client] LoadAtomicFile para ID SA %d", modelId);
        loadOk = tryLoadAtomic(stream, static_cast<uint32_t>(modelId), &clumpExc);
        gta::RwStreamClose(stream, nullptr);
        clump = reinterpret_cast<gta::RpClump*>(
            gta::modelField<gta::RwObject*>(newInfo, gta::OFF_RW_OBJECT));
    } else {
        gta::RwStreamClose(stream, nullptr);
        clump = tryRpClumpRead2(absDff.c_str(), &clumpExc);
        loadOk = (clump != nullptr);
    }

    *reinterpret_cast<void**>(0x9689E0) = previousCurrentInfo;

    if (!loadOk || !clump) {
        logMsg("[cv_client] Fallo al cargar DFF para modelo %d (exception=0x%08X)",
               modelId, clumpExc);
        gta::RemoveTxdSlot(txdSlot);
        rollbackModelInfo();
        return false;
    }

    gta::modelField<gta::RwObject*>(newInfo, gta::OFF_RW_OBJECT) =
        reinterpret_cast<gta::RwObject*>(clump);

    logMsg("[cv_client] Modelo %d registrado OK (%s)", modelId, dffPath.c_str());
    return true;
}'''
loader = loader[:reg_start] + reg_replacement + loader[reg_end:]

write(loader_path, loader)
print("Parche aplicado: descarga fuera de DllMain, carga desde INI, sin editar gta3.img y rollback seguro del model-info.")
