# San Andreas Horizon - BMX personalizada

## Recursos publicados

- [bmx.dff](https://raw.githubusercontent.com/boruto4713-creator/SanAndreasHorizon-Assets/main/bmx.dff)
- [bmx.txd](https://raw.githubusercontent.com/boruto4713-creator/SanAndreasHorizon-Assets/main/bmx.txd)

Tamanos verificados en GitHub: DFF 16,250,880 bytes; TXD 563,200 bytes.

## Modelo de Horizon

El gamemode existente crea las diez bicicletas con `CreateVehicle(481)`.
La sustitucion buscada es solo visual: mantener el ID 481 y las fisicas originales.
No se debe cambiar `Horizon.pwn` ni la skin de la recepcionista.

## Configuracion de descarga preparada

`client/custom_vehicles.ini` contiene los enlaces HTTP directos y la ruta local esperada por el descargador ASI de referencia. Debe copiarse al directorio de GTA SA junto a un ASI compatible, cuando exista un build corregido.

## Estado tecnico: pendiente

El repositorio de referencia [YeyoDiego223/samp-custom-vehicles](https://github.com/YeyoDiego223/samp-custom-vehicles) no esta listo para instalarse tal cual:

- La funcion cliente `loadConfig()` que registra el modelo se define, pero no aparece invocada en el codigo publicado.
- La rutina de parcheo actual solo parchea el DFF en `gta3.img`; el propio codigo registra que el parcheo del TXD queda para una version futura.
- El plugin Pawn `RegisterVehicleModel` guarda datos en una tabla local del servidor; el codigo publicado no implementa el envio de ese registro al cliente.
- El ASI usa direcciones fijas de memoria para GTA San Andreas 1.0 US y modifica `models/gta3.img`.

Por estos motivos no se incluye un DLL/ASI compilado ni se recomienda instalar el binario de una fuente no verificada. La configuracion y los modelos estan alojados, pero la sustitucion automatica aun no esta operativa.

## Requisitos que declara el proyecto de referencia

- GTA San Andreas 1.0 US.
- Cliente SA-MP 0.3.DL-R1.
- ASI loader.
- Un ASI corregido y probado con el DFF/TXD de Horizon.

El servidor Horizon usa open.mp. El plugin Pawn de referencia no debe agregarse a `config.json` hasta demostrar su compatibilidad y corregir la comunicacion cliente-servidor.
