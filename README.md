# RamExpert2

Programa para Windows que muestra la memoria RAM del equipo: las ranuras ocupadas y libres, los módulos instalados y qué memoria comprar para ampliarlo.

## Qué muestra

- **Resumen:** memoria instalada, ranuras, capacidad máxima, tipo y uso de memoria en vivo.
- **Cada ranura:** fabricante, número de pieza y de serie, velocidad (con su código, por ejemplo PC4-25600), voltaje y rangos.
- **Requisitos para ampliar:** tipo, formato y pines, velocidad mínima, voltaje, capacidad por módulo y la mejor configuración posible. Se pueden copiar o buscar módulos compatibles con un clic.
- **Menú Ver:** uso de memoria en tiempo real, información del sistema y consejos de ampliación.
- **Menú Archivo:** informe completo en texto para copiar o guardar.

Los datos se leen de la tabla SMBIOS del firmware, la misma fuente que usan RamExpert o CPU-Z. No necesita permisos de administrador ni conexión a internet.

## Descargar

1. En [Releases](https://github.com/Enrique-GuzRam/Ramexpert2safe/releases), descarga `RamExpert2-v1.0.0-win-x64.zip`.
2. Descomprímelo y abre `RamExpert2.exe`.

No hace falta instalar nada: el .zip ya incluye .NET y el Windows App SDK. Funciona en Windows 10 (1809 o posterior) y Windows 11 de 64 bits.

El programa no está firmado digitalmente, así que la primera vez Windows SmartScreen puede mostrar «Windows protegió su PC». Para abrirlo, pulsa «Más información» y luego «Ejecutar de todas formas». En las notas de cada versión está el SHA-256 del .zip, para comprobar que el archivo descargado es el original:

```powershell
Get-FileHash .\RamExpert2-v1.0.0-win-x64.zip -Algorithm SHA256
```

## Compilar desde el código

Necesitas el [SDK de .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet run                                   # ejecutar en modo depuración
dotnet publish -c Release -p:Platform=x64    # versión autocontenida para distribuir
```

La versión publicada queda en `bin\Release\net8.0-windows10.0.26100.0\win-x64\publish\`.

## Estructura

- `Hardware/`: lectura de la tabla SMBIOS y del uso de memoria (`GetSystemFirmwareTable` y `GlobalMemoryStatusEx`).
- `ViewModels/`: datos formateados para la interfaz y lógica de compatibilidad (`UpgradeAdvisor.cs`).
- `MainPage.xaml`: la interfaz, hecha con WinUI 3.
