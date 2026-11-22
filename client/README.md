# San Andreas Horizon - BMX personalizada

## Recursos publicados

- [bmx.dff](https://raw.githubusercontent.com/boruto4713-creator/SanAndreasHorizon-Assets/main/bmx.dff)
- [bmx.txd](https://raw.githubusercontent.com/boruto4713-creator/SanAndreasHorizon-Assets/main/bmx.txd)

Tamanos verificados: DFF 16,250,880 bytes; TXD 563,200 bytes.

## Modelo de Horizon

El gamemode crea las bicicletas con `CreateVehicle(481)`. El objetivo es sustituir solo su apariencia, manteniendo el ID y las propiedades nativas del vehiculo. No se modifica `Horizon.pwn`, el sistema de la recepcionista ni `config.json`.

## Cliente compilado

El workflow de GitHub Actions compila `custom_vehicles.asi` para Windows de 32 bits. El cargador se modifica para:

- descargar los DFF/TXD desde las URLs de este repositorio fuera de `DllMain`;
- leer `custom_vehicles.ini` al inicializar el juego y registrar el modelo 481;
- no editar `models/gta3.img`;
- restaurar los campos principales del model-info original si falla la carga.

El paquete se genera como artefacto de GitHub Actions llamado `Horizon-BMX-Client`.

## Instalacion para cada jugador

Requisitos declarados por el proyecto base: GTA San Andreas 1.0 US, SA-MP 0.3.DL-R1 y un ASI Loader compatible.

Cada jugador debe instalar una sola vez `custom_vehicles.asi` y `custom_vehicles.ini` en la carpeta raiz del GTA (junto a `gta_sa.exe`). Despues el ASI descarga el DFF y el TXD desde GitHub a `models/custom`. La descarga de los modelos es automatica; el ASI y el INI son la instalacion inicial.

No se agrega ningun DLL al servidor open.mp: el reemplazo de un slot existente, 481, se realiza en el cliente.

## Estado y limitaciones

- La compilacion de Windows x86 del ASI paso en GitHub Actions.
- **Aun falta probarlo dentro de GTA SA** con un cliente 1.0 US + SA-MP 0.3.DL-R1. Una compilacion correcta no garantiza que las direcciones internas y la carga RenderWare funcionen en todos los clientes.
- El proyecto base usa direcciones fijas de memoria. No usar con versiones Steam, 1.01, compactadas o ejecutables distintos a GTA SA 1.0 US.
- Antes de probar, hacer copia de seguridad de la carpeta GTA. Esta version no modifica `gta3.img`.

El proyecto fuente de referencia es [YeyoDiego223/samp-custom-vehicles](https://github.com/YeyoDiego223/samp-custom-vehicles), fijado al commit `f88d8e19ceff350a477925a7bd668353cd305f79`.
