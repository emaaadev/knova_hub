# Knova Hub

## ¿Qué es y para qué sirve?

**Knova Hub** es un Sistema Inteligente de Gestión del Conocimiento Empresarial. 

Sirve como una plataforma centralizada diseñada para capturar, organizar y recomendar el conocimiento interno de una organización mediante el uso de inteligencia artificial. Su objetivo principal es evitar que la información valiosa se pierda en hilos de correos o archivos aislados, transformando los datos de la empresa en un activo accesible y útil para cualquier miembro del equipo en el momento en que lo necesite.

## Funciones Principales

* **Captura centralizada:** Permite integrar documentos, wikis internas, minutas y otros recursos en un solo lugar.
* **Organización automática:** Utiliza procesamiento de lenguaje natural para etiquetar, categorizar y estructurar la información sin esfuerzo manual.
* **Recomendaciones proactivas:** La IA sugiere artículos o soluciones basándose en el contexto del proyecto actual del usuario.
* **Búsqueda semántica:** Permite a los usuarios hacer preguntas en lenguaje natural para encontrar respuestas precisas dentro de los documentos de la empresa, en lugar de depender de palabras clave exactas.

## Arquitectura y Estructura del Proyecto

El proyecto sigue una arquitectura Cliente-Servidor. El backend está construido en **C# (.NET)** implementando una rigurosa **Arquitectura de N-Capas (N-Tier Architecture)** para garantizar un alto grado de escalabilidad, mantenibilidad y el cumplimiento del principio de separación de responsabilidades. El cliente web está desarrollado de manera ágil utilizando **JavaScript Vanilla**.

A continuación, se detalla la estructura principal del repositorio:

```text
📁 KnovaHub/
├── 📁 Client-Js                     # Frontend
├── 📁 KnovaHub.API                  # Backend: Capa de Presentación (API)
├── 📁 KnovaHub.ApplicationLayer     # Backend: Capa de Aplicación
├── 📁 KnovaHub.DomainLayer          # Backend: Capa de Dominio (Núcleo)
└── 📁 KnovaHub.InfrastructureLayer  # Backend: Capa de Infraestructura
```


## Configuración local: clave JWT

La clave para firmar los tokens **no está en el repositorio**. Cada desarrollador debe configurarla en su máquina con User Secrets.

1. Genera una clave (mínimo 32 caracteres). En PowerShell:

   ```powershell
   $b = New-Object byte[] 48; [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b)
   ```

2. Guárdala:
   - **Visual Studio:** clic derecho en `KnovaHub.API` > *Administrar secretos de usuario* y agrega `"Jwt:Key": "tu-clave"`.
   - **Terminal**, desde la carpeta `KnovaHub.API`:

     ```
     dotnet user-secrets set "Jwt:Key" "tu-clave"
     ```

3. Ejecuta la API en modo desarrollo. Si la clave falta, la API no inicia y muestra un mensaje indicándolo.

En producción, la clave se pasa con la variable de entorno `Jwt__Key`.
