# San Andreas Horizon

Launcher de Windows para preparar el cliente de San Andreas Horizon y aplicar la BMX personalizada sin usar ASI.

## Launcher

El proyecto está en la carpeta launcher y se compila con GitHub Actions mediante .github/workflows/build-launcher.yml.

El paquete de Windows se publica como artefacto de GitHub Actions:

- Nombre: SanAndreasHorizon-Launcher-win-x64
- Ejecutable: SanAndreasHorizonLauncher.exe
- Plataforma: Windows x64, .NET 8 autocontenido

## BMX personalizada sin ASI

El launcher descarga bmx.dff y bmx.txd desde este repositorio, verifica sus tamaños y SHA-256 y reemplaza las entradas con esos nombres dentro de models/gta3.img.

No utiliza .asi, ASI Loader, plugin de servidor ni cambios en Horizon.pwn. El ID de vehículo 481 permanece igual: se cambia la geometría y la textura asociadas al modelo BMX del juego.

### Copia de seguridad y restauración

Antes de instalar por primera vez, crea:

models/gta3.img.horizon-original.bak

El botón RESTAURAR ORIGINAL repone esa copia. El launcher no sobrescribe la copia original en las actualizaciones posteriores.

### Requisitos y límites

- GTA San Andreas para Windows, con gta_sa.exe y models/gta3.img.
- El IMG debe ser formato VER2 de GTA San Andreas y contener exactamente una entrada bmx.dff y una bmx.txd.
- Cierra GTA antes de instalar o restaurar.
- La carpeta del juego debe permitir escritura. El launcher no solicita privilegios de administrador automáticamente.
- Los jugadores deben aplicar el modelo en sus propias instalaciones. El servidor open.mp no entrega por sí solo estos cambios locales del archivo IMG.
- La primera compilación debe probarse en una instalación real de GTA antes de distribuirla como versión estable. No se da por comprobada la compatibilidad con instalaciones modificadas o con archivos IMG alterados por otros mods.

## Conexión

El launcher permite configurar host, puerto, nombre y ejecutable del cliente. Para open.mp usa los argumentos -h, -p, -n y -g; para SA-MP usa -h, -p y -n.

El host inicial es 127.0.0.1, que solo sirve para conectar desde el mismo equipo donde corre el servidor. Para otros jugadores debe configurarse la dirección de red accesible del servidor.
