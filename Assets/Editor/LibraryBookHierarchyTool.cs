using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Audita los conjuntos de libros de la escena abierta sin modificar su jerarquía.</summary>
public static class LibraryBookHierarchyTool
{
    private readonly struct Placement
    {
        public readonly Transform Transform;
        public readonly Matrix4x4 Matrix;
        public readonly Renderer Renderer;
        public readonly Bounds Bounds;

        public Placement(Transform transform)
        {
            Transform = transform;
            Matrix = transform.localToWorldMatrix;
            Renderer = transform.GetComponent<Renderer>();
            Bounds = Renderer != null ? Renderer.bounds : default;
        }

        public bool IsUnchanged()
        {
            if (Transform == null) return false;
            Matrix4x4 current = Transform.localToWorldMatrix;
            for (int i = 0; i < 16; i++)
                if (Mathf.Abs(Matrix[i] - current[i]) > 0.0001f) return false;
            if (Renderer == null) return true;
            return (Renderer.bounds.center - Bounds.center).sqrMagnitude < 0.00000001f
                && (Renderer.bounds.size - Bounds.size).sqrMagnitude < 0.00000001f;
        }
    }

    [MenuItem("The Lights Before Dawn/Arte/Anidar libros en bibliotecas")]
    private static void ParentBooks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Salí de Play antes de anidar los libros.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/ModularPrototype.unity")
        {
            Debug.LogError("Abrí ModularPrototype antes de anidar los libros.");
            return;
        }

        Transform pool = scene.GetRootGameObjects().Single(go => go.name == "POOL_HABITACIONES").transform;
        Transform furniture = pool.Find("Habitacion_Biblioteca/MOBILIARIO_GREYBOX");
        Transform central = furniture != null ? furniture.Find("Biblioteca_CentralDobleCara") : null;
        Transform southRight = furniture != null ? furniture.Find("Biblioteca_SurDerecha") : null;
        if (central == null || southRight == null)
            throw new InvalidOperationException("No se encontraron las dos bibliotecas de destino.");

        Transform[] looseBooks = pool.Cast<Transform>()
            .Where(child => child.name.StartsWith("book_", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Transform importedSet = scene.GetRootGameObjects()
            .SingleOrDefault(go => go.name == "low_poly_books")?.transform;
        Renderer[] importedPieces = importedSet != null
            ? importedSet.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();

        if (looseBooks.Length == 0 && importedPieces.Length == 0)
        {
            Debug.Log("Los libros auditados ya están anidados en sus bibliotecas.");
            return;
        }
        if (looseBooks.Length != 37 || importedPieces.Length != 7)
            throw new InvalidOperationException("La escena cambió desde la auditoría: "
                + looseBooks.Length + " libros sueltos y " + importedPieces.Length + " piezas importadas.");

        Bounds? centralBounds = LibraryGeometry(central);
        Bounds? southBounds = LibraryGeometry(southRight);
        if (!centralBounds.HasValue || !southBounds.HasValue)
            throw new InvalidOperationException("Falta geometría de una biblioteca de destino.");

        var destinations = new Dictionary<Transform, Transform>();
        foreach (Transform book in looseBooks)
        {
            Renderer renderer = book.GetComponent<Renderer>();
            Vector3 point = renderer != null ? renderer.bounds.center : book.position;
            if (HorizontalDistance(centralBounds, point) > .04f)
                throw new InvalidOperationException("Libro fuera de la biblioteca central: " + book.name);
            destinations.Add(book, central);
        }
        foreach (Renderer piece in importedPieces)
        {
            Transform destination = HorizontalDistance(centralBounds, piece.bounds.center)
                <= HorizontalDistance(southBounds, piece.bounds.center) ? central : southRight;
            if (HorizontalDistance(LibraryGeometry(destination), piece.bounds.center) > .04f)
                throw new InvalidOperationException("Pieza importada fuera de la biblioteca: " + piece.name);
            destinations.Add(piece.transform, destination);
        }
        if (destinations.Values.Count(target => target == central) != 40
            || destinations.Values.Count(target => target == southRight) != 4)
            throw new InvalidOperationException("La distribución de libros cambió desde la auditoría.");

        Placement[] before = destinations.Keys.SelectMany(root =>
            root.GetComponentsInChildren<Transform>(true).Select(child => new Placement(child))).ToArray();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Anidar libros en bibliotecas");
        try
        {
            if (PrefabUtility.IsPartOfPrefabInstance(importedSet)
                && PrefabUtility.GetNearestPrefabInstanceRoot(importedSet.gameObject) == importedSet.gameObject)
                PrefabUtility.UnpackPrefabInstance(importedSet.gameObject,
                    PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            foreach (var entry in destinations)
                Undo.SetTransformParent(entry.Key, entry.Value, "Anidar libro en biblioteca");

            if (before.Any(placement => !placement.IsUnchanged()))
                throw new InvalidOperationException("La ubicación o geometría cambió durante el anidado.");

            // El prefabricado importado puede dejar envoltorios vacíos tras separar las mallas.
            if (importedSet != null && importedSet.GetComponentsInChildren<Component>(true)
                .All(component => component is Transform))
                Undo.DestroyObjectImmediate(importedSet.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("Se anidaron 37 libros sueltos y 7 piezas low poly: "
                + "40 en Biblioteca_CentralDobleCara y 4 en Biblioteca_SurDerecha. "
                + "Las matrices mundiales y los límites visuales se conservaron.");
        }
        catch
        {
            Undo.RevertAllDownToGroup(undoGroup);
            throw;
        }
    }

    [MenuItem("The Lights Before Dawn/Arte/Copiar auditoría de libros")]
    private static void CopyAudit()
    {
        Scene scene = SceneManager.GetActiveScene();
        var objects = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .ToArray();
        var libraries = objects.Where(t => t.name.StartsWith("Biblioteca_", StringComparison.Ordinal)
                && Path(t).StartsWith("POOL_HABITACIONES/Habitacion_Biblioteca/", StringComparison.Ordinal))
            .OrderBy(t => t.name).ToArray();
        var bookRoots = objects.Where(t => IsBook(t.name) && !HasBookAncestor(t)
                && (Path(t).StartsWith("POOL_HABITACIONES/", StringComparison.Ordinal)
                    || t.name.StartsWith("low_poly_books", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(t => t.name).ToArray();

        var report = new StringBuilder();
        report.AppendLine("Escena: " + scene.path);
        report.AppendLine("Bibliotecas: " + libraries.Length + "; raíces de libros: " + bookRoots.Length);
        foreach (Transform library in libraries)
        {
            Bounds? geometry = LibraryGeometry(library);
            report.AppendLine("BIBLIOTECA " + Path(library) + " | posición " + library.position
                + " | geometría " + (geometry.HasValue ? geometry.Value.ToString() : "sin renderer"));
        }
        foreach (Transform book in bookRoots)
        {
            Transform library = libraries.FirstOrDefault(candidate => book.IsChildOf(candidate));
            Renderer[] renderers = book.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(book.position, Vector3.zero);
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            Transform nearest = libraries.OrderBy(candidate => HorizontalDistance(
                LibraryGeometry(candidate), bounds.center)).FirstOrDefault();
            if (library != null) continue;
            report.AppendLine("LIBRO " + Path(book) + " | biblioteca "
                + (library != null ? library.name : "NINGUNA") + " | posición " + book.position
                + " | centro visual " + bounds.center + " | tamaño visual " + bounds.size
                + (library == null ? " | biblioteca cercana " + (nearest != null ? nearest.name : "NINGUNA") : ""));
        }
        Transform importedSet = objects.FirstOrDefault(t => t.name == "low_poly_books" && t.parent == null);
        if (importedSet != null)
            foreach (Renderer renderer in importedSet.GetComponentsInChildren<Renderer>(true))
                report.AppendLine("PIEZA LOW_POLY " + Path(renderer.transform)
                    + " | centro visual " + renderer.bounds.center
                    + " | tamaño visual " + renderer.bounds.size);

        GUIUtility.systemCopyBuffer = report.ToString();
        Debug.Log("Auditoría de libros copiada al portapapeles: " + bookRoots.Length + " conjuntos.");
    }

    private static bool IsBook(string name)
        => name.StartsWith("book_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("book2_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("books_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("books.001_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("low_poly_books", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("LibrosDecorativos_", StringComparison.Ordinal)
            || name.StartsWith("Enciclopedias_", StringComparison.Ordinal);

    private static bool HasBookAncestor(Transform child)
    {
        for (Transform parent = child.parent; parent != null; parent = parent.parent)
            if (IsBook(parent.name)) return true;
        return false;
    }

    private static Bounds? LibraryGeometry(Transform library)
    {
        Renderer[] renderers = library.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => !IsBook(renderer.name) && !HasBookAncestor(renderer.transform))
            .ToArray();
        if (renderers.Length == 0) return null;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static float HorizontalDistance(Bounds? bounds, Vector3 point)
    {
        if (!bounds.HasValue) return float.PositiveInfinity;
        Vector3 nearest = bounds.Value.ClosestPoint(point);
        nearest.y = point.y;
        return (nearest - point).sqrMagnitude;
    }

    private static string Path(Transform child)
    {
        string path = child.name;
        for (Transform parent = child.parent; parent != null; parent = parent.parent)
            path = parent.name + "/" + path;
        return path;
    }
}
