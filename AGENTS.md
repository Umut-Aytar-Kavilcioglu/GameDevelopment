# AGENTS.md

## Project Overview

This repository contains a custom 2D game framework/engine written in C#.

The goal is to build a relatively small, understandable, and deliberately designed framework rather than a large general-purpose engine.

The project should favor clear architecture, predictable behavior, explicit ownership, and maintainable code over excessive abstraction or premature generalization.

Codex should treat this repository as an evolving engine architecture, not merely as a collection of coding tasks.

## Technology Direction

The project is built around:

* C# / .NET
* SDL3 for platform-level functionality
* SDL3 GPU API for rendering
* Arch as the ECS implementation

SDL3 is accessed through the project's SDL3 C# bindings.

Do not introduce another graphics or windowing abstraction such as MonoGame, FNA, Raylib, OpenTK, or similar frameworks unless explicitly requested.

Rendering should be designed around the SDL3 GPU API rather than SDL's legacy renderer API.

Arch should remain the ECS foundation. Do not introduce a second ECS implementation or build a competing custom ECS layer.

## Architecture Philosophy

Keep the engine architecture small and intentional.

Prefer:

* explicit systems over hidden behavior
* composition over deep inheritance hierarchies
* data-oriented ECS components
* clear ownership of native and GPU resources
* simple APIs on the game-facing side
* separation between engine internals and game code
* abstractions that solve an actual current problem

Avoid:

* speculative abstractions
* unnecessary interfaces
* excessive generic infrastructure
* service-locator style global architecture
* large inheritance trees
* abstractions created only because they might theoretically be useful later

Do not introduce new architectural layers without a concrete reason.

When several designs are reasonable, prefer the smallest design that preserves a clean future evolution path.

## Engine and Game Boundary

The engine/framework should provide reusable infrastructure and game-facing APIs.

Game-specific behavior should remain outside the engine whenever possible.

Engine internals may use SDL3, Arch, native handles, GPU resources, and other low-level implementation details.

Game-facing APIs should expose engine concepts rather than unnecessarily leaking SDL3 implementation details.

A useful distinction is:

Game code expresses what it wants to do.

Engine internals determine how that is implemented.

This boundary should remain pragmatic rather than dogmatic. Do not create wrappers merely to hide every SDL type.

## ECS Direction

Entities are composed from components and processed by systems using Arch.

Components should primarily represent state and data.

Systems should contain behavior that operates across relevant entities.

Do not automatically make every engine concept an ECS component.

Resources, rendering infrastructure, asset management, GPU objects, engine services, and similar concepts may exist outside the ECS when that produces a clearer design.

Avoid placing substantial behavior directly inside data components.

System ordering should be explicit where execution order affects behavior.

## Rendering Direction

The engine's 2D rendering layer is conceptually exposed through `Renderer2D`.

`Renderer2D` should provide a simple and engine-oriented 2D rendering API while using the SDL3 GPU API internally.

Ordinary game code should not need to work directly with SDL GPU command buffers, render passes, native GPU handles, transfer buffers, or similar low-level concepts unless explicitly working at a lower engine layer.

Rendering infrastructure should preserve clear separation between:

* game-facing rendering concepts
* engine rendering orchestration
* low-level SDL3 GPU interaction

Do not use SDL's legacy renderer as the foundation of `Renderer2D`.

Do not introduce another graphics backend unless explicitly requested.

The exact rendering architecture, batching strategy, resource model, and public APIs should evolve from actual engine requirements rather than being prematurely generalized.

## Resource Ownership

Native SDL resources and GPU resources must have clear ownership and lifetime rules.

Whenever introducing a resource type, determine:

* who creates it
* who owns it
* who may reference it
* when it is destroyed
* whether destruction is deterministic
* whether the resource can outlive the SDL or GPU context it depends on

Avoid designs where ownership is ambiguous.

Do not casually duplicate native resource handles.

## Public API Design

Game-facing APIs should be concise and pleasant to use.

Prefer APIs that communicate engine concepts clearly rather than requiring ordinary game code to manipulate SDL handles or low-level GPU structures directly.

Avoid elaborate fluent APIs or abstraction layers before their requirements exist.

Public API names should be stable, understandable, and idiomatic C#.

Internal implementation details may remain lower level.

## Dependency Policy

Prefer a small dependency surface.

Existing foundational dependencies such as SDL3 and Arch should be used directly and intentionally instead of introducing additional libraries that duplicate their responsibilities.

Before adding a new dependency, consider whether the functionality is small enough to implement cleanly within the project.

Do not replace an established project dependency without discussing the architectural consequences first.

## Coding Approach

Before making substantial changes:

1. Inspect the relevant existing code.
2. Understand how the change fits the current architecture.
3. Identify resource ownership and subsystem boundaries where relevant.
4. Prefer extending existing concepts over creating parallel ones.
5. Keep the change scoped to the requested problem.

Do not perform unrelated refactors while implementing a feature unless they are necessary for correctness.

Do not rewrite working systems solely to match a different architectural preference.

## Architectural Discussions

Codex is expected to participate in architectural reasoning, not only generate code.

When asked about architecture:

* analyze tradeoffs
* challenge assumptions when useful
* identify coupling and ownership implications
* distinguish current requirements from hypothetical future requirements
* offer alternatives when there are meaningful design choices
* explain why one approach fits this project better

Do not automatically produce implementation code when the question is primarily architectural.

It is acceptable to recommend postponing a decision when the requirements are not yet strong enough to justify an abstraction.

When a decision affects foundational architecture, discuss it before silently encoding it into the implementation.

## Working With Existing Decisions

Treat architectural decisions already represented in the repository as intentional unless there is evidence otherwise.

Before proposing a replacement, understand why the current structure exists.

If a requested change conflicts with an existing architectural direction, point out the conflict rather than silently creating an inconsistent implementation.

Architecture may evolve, but changes should be deliberate.

## Generated and Third-Party Code

Do not casually modify generated or externally sourced binding code.

SDL3 binding files may closely mirror upstream/native APIs and can contain unsafe code, native handles, marshalling declarations, generated structures, and low-level interop definitions.

Treat such files primarily as interoperability infrastructure rather than ordinary engine code.

Prefer implementing engine abstractions outside the binding layer unless an actual binding correction is required.

## Performance

This is a game framework, so allocations and unnecessary work in hot paths matter.

However, do not sacrifice architecture or readability for speculative micro-optimizations.

Pay particular attention to:

* per-frame allocations
* ECS iteration
* rendering submission
* native interop
* GPU resource creation and destruction
* unnecessary copying of large structures

Optimize based on architecture and measurable hot paths rather than intuition alone.

## Error Handling

Native SDL failures should provide enough context to diagnose the failure.

When an SDL operation can fail, preserve useful SDL error information where appropriate.

Do not silently ignore native failures that leave the engine in an invalid state.

At the same time, avoid wrapping every SDL call in excessive defensive infrastructure.

## Scope of This File

This file documents project-wide direction and architectural principles.

It intentionally does not define every component, system, resource type, class, or implementation detail.

The repository itself is the source of truth for the current implementation.

When this file and the implementation appear inconsistent, inspect the surrounding code and discuss the discrepancy rather than blindly assuming either side is correct.

Detailed subsystem decisions should live in the implementation, architectural discussions, or dedicated documentation rather than continuously expanding this file.
