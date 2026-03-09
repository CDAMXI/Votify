# Votify 🏆
> *"Juzgar y competir en un solo clic"*

Plataforma inteligente de gestión de votaciones para entornos competitivos y colaborativos (hackathons, ferias de innovación, competiciones, etc.). Votify reemplaza hojas de cálculo y procesos manuales con una experiencia inmersiva de evaluación analítica en tiempo real.

---

## Índice

- [Descripción del producto](#descripción-del-producto)
- [Roles del sistema](#roles-del-sistema)
- [Funcionalidades principales](#funcionalidades-principales)
- [Tecnologías](#tecnologías)
- [Metodología de desarrollo](#metodología-de-desarrollo)
- [Estructura del proyecto](#estructura-del-proyecto)
- [Instalación y ejecución](#instalación-y-ejecución)
- [Equipo](#equipo)

---

## Descripción del producto

Votify orquesta eventos con **competidores, premios, proyectos, roles, reglas y ventanas de tiempo**. A diferencia de soluciones genéricas del mercado (Award Force, Evalato, Slido, etc.), Votify está diseñado específicamente para cubrir las necesidades de eventos competitivos de pequeña y mediana escala, con foco en:

- Retroalimentación detallada y en tiempo real para participantes.
- Configuración flexible de baremos y criterios de evaluación.
- Transparencia en el proceso de votación con trazabilidad completa.
- Interfaz adaptativa según rol y dispositivo (web, móvil, realidad aumentada).

---

## Roles del sistema

| Rol | Descripción |
|---|---|
| **Organizador** | Configura el evento, define categorías, premios, criterios y ventanas de tiempo. Gestiona el acceso de los demás roles. |
| **Jurado** | Evalúa proyectos mediante votaciones multicriterio (numéricas, rúbricas, checklists, comentarios, audio, vídeo). |
| **Competidor** | Participa en el evento con un proyecto. Accede a su dashboard de evaluación y retroalimentación. |
| **Votante** | Emite votos en categorías habilitadas. Un competidor puede actuar también como votante (configurable). |

> Un mismo usuario puede desempeñar varios roles simultáneamente según la configuración del evento.

---

## Funcionalidades principales

### Gestión de eventos
- Creación y configuración de eventos con múltiples categorías y premios.
- Definición de baremos de evaluación con criterios ponderados.
- Plantillas de baremos y sugerencia automática de criterios mediante IA.
- Control de ventanas de tiempo para votaciones (minutos, horas o días).

### Votación y evaluación
- Votaciones multicriterio: numéricas, checklists, rúbricas, comentarios, audio y vídeo.
- Votación por puntos distribuibles con configuración flexible (X proyectos, Y puntos, etc.).
- Restricción de un voto por persona por categoría.
- Configuración de si un competidor puede votar por su propio proyecto.
- Supervisión del estado de votaciones en tiempo real.
- Extensión o cierre anticipado de períodos de votación.
- Notificaciones opcionales al abrirse votaciones y alertas antes del cierre.

### Retroalimentación e IA
- Dashboard por competidor con puntuaciones desglosadas por dimensión en tiempo real.
- Síntesis automática de comentarios cualitativos del jurado mediante modelos de lenguaje.
- Generación de hoja de ruta de mejora personalizada por participante.
- Anonimato de evaluadores garantizado con trazabilidad interna completa.
- Síntesis preliminar y final para el jurado antes de la puesta en común.

### Resultados y certificados
- Ranking según baremo con posibilidad de intervención manual del jurado.
- Generación automática de certificados para ganadores y participantes.
- Publicación automática de resultados al público interesado.
- Votaciones inmutables con validación distribuida; auditoría completa disponible.

### Dispositivos e interfaz
- IU adaptativa según rol y dispositivo (web, móvil, realidad aumentada).
- Sincronización en tiempo real de cambios de configuración en todos los dispositivos conectados.

---

## Tecnologías

```
Frontend:   EntityFramework
Backend:    C#
Base de datos: SupaBase
Control de versiones: Git / GitHub
Gestión de proyecto: Worki (TUNE-UP Process)
```

---

## Metodología de desarrollo

El proyecto se desarrolla con **TUNE-UP Process**, una configuración ágil que integra Scrum, Kanban, XP y Lean Development, apoyada con la herramienta **Worki**.

### Sprints

| Sprint | Contenido |
|---|---|
| **Sprint 0** | Puesta en marcha: modelo de dominio, backlog inicial, planificación Sprint 1. |
| **Sprint 1** | Primer incremento funcional del producto. |
| **Sprint 2** | Segundo incremento. Preparación Sprint 3. |
| **Sprint 3** | Entrega final. Versión operativa del producto. |

### Tipos de Unidades de Trabajo (UT)

- **Nuevo Requisito**: funcionalidad nueva o modificación significativa.
- **Mejora**: cambio menor sobre funcionalidad ya implementada.
- **Corrección de Fallo**: bug fix (severidad: Leve / Moderado / Severo / Crítico).
- **Otras tareas**: trabajo sin impacto en el comportamiento del producto.

### Estimación de capacidad

El esfuerzo se estima en **HIP (Horas Ideales de Programación)**. Cada integrante del equipo dispone de **4 HIP/semana**. Se aplica una holgura del 10% sobre la capacidad teórica del equipo.

---

## Estructura del proyecto

```
votify/
├── docs/               # Documentación del proyecto (modelo de dominio, mockups, etc.)
├── src/                # Código fuente
│   ├── frontend/
│   └── backend/
├── tests/              # Pruebas de aceptación y unitarias
├── scripts/            # Scripts de utilidad
└── README.md
```

---

## Instalación y ejecución

> ⚠️ Esta sección debe completarse por el equipo al finalizar el Sprint 1.

```bash
# Clonar el repositorio
git clone https://github.com/[organización]/votify.git

# Instrucciones de instalación pendientes
```

*Proyecto académico desarrollado en ETSINF – Universitat Politècnica de València.*
