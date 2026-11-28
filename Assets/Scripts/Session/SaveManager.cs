using System;
using UnityEngine;

namespace MyFreshMushroom
{
    [DisallowMultipleComponent]
    public sealed class SaveManager : MonoBehaviour
    {
        [SerializeField, Min(1)] private float autosaveIntervalSeconds = 120;

        private const double CheckpointIntervalSeconds = 5;
        private SessionSaveStore store;
        private GameManager session;
        private ForestGridView forest;
        private double nextAutosaveAt;
        private double nextCheckpointAt;
        private bool dirty;
        private bool checkpointRequested;

        internal static string SaveDirectoryOverride { get; set; }
        internal bool IsResetting { get; private set; }

        private static SessionSaveStore CreateStore()
        {
            string directory = SaveDirectoryOverride ?? Application.persistentDataPath;
            return string.IsNullOrEmpty(directory) ? null : new SessionSaveStore(directory);
        }

        internal static bool HasSaveFiles() => CreateStore()?.HasSaveFiles == true;

        internal void PrepareStore() => store = CreateStore();

        internal RestoredSession Load(Func<SessionSaveData, RestoredSession> restore) => store?.Load(restore);

        internal bool WriteStartingSnapshot(Func<SessionSaveData> capture) =>
            store == null || store.Write(capture(), reset: true);

        internal void TrackSession(GameManager game, ForestGridView forestView, bool loadedSave)
        {
            if (store == null || store.HasNewerSave) return;
            session = game;
            forest = forestView;
            session.Inventory.Changed += RequestCheckpoint;
            session.Progress.DiscoveryChanged += RequestCheckpoint;
            session.FacilityLayoutChanged += RequestCheckpoint;
            session.Cultivation.Watered += OnWatered;
            session.Cultivation.TimeAdvanced += MarkDirty;
            session.Exploration.TimeAdvanced += MarkDirty;
            session.Exploration.JourneyChanged += RequestCheckpoint;
            if (session.Tutorial != null) session.Tutorial.ProgressChanged += RequestCheckpoint;

            SetSaveDeadlines();
            if (!loadedSave || store.NeedsRepair)
            {
                RequestCheckpoint();
                nextCheckpointAt = Time.unscaledTimeAsDouble;
            }
        }

        internal void RequestCheckpoint()
        {
            if (store == null || store.HasNewerSave || IsResetting) return;
            dirty = checkpointRequested = true;
        }

        private void OnWatered(FacilityState _) => RequestCheckpoint();
        private void MarkDirty() => dirty = true;

        private void SetSaveDeadlines()
        {
            double now = Time.unscaledTimeAsDouble;
            nextAutosaveAt = now + Mathf.Max(1, autosaveIntervalSeconds);
            nextCheckpointAt = now + CheckpointIntervalSeconds;
        }

        private void LateUpdate()
        {
            double now = Time.unscaledTimeAsDouble;
            if (dirty && !session.IsAtTitle && (now >= nextAutosaveAt || checkpointRequested && now >= nextCheckpointAt))
                Flush();
        }

        internal bool Flush()
        {
            if (!dirty || IsResetting) return true;
            if (session.PendingJourney != null || session.Cultivation.IsProcessingAction) return false;

            var data = SessionSaveData.Capture(session.Inventory, session.Progress, forest.Selection.Registered,
                session.Mushrooms, session.ForestAreas.Values, session.Tutorial?.Capture() ?? new TutorialSaveData(),
                session.Exploration);
            SetSaveDeadlines();
            if (!store.Write(data)) return false;
            dirty = checkpointRequested = false;
            return true;
        }

        internal bool TryReset(Func<SessionSaveData> createStartingSnapshot)
        {
            if (IsResetting) return false;
            // Keep the old session blocked until its scene has been replaced.
            IsResetting = true;
            bool resetSaved = false;
            try
            {
                resetSaved = WriteStartingSnapshot(createStartingSnapshot);
                return resetSaved;
            }
            finally
            {
                if (!resetSaved) IsResetting = false;
            }
        }

        private void OnApplicationQuit() => Flush();

        private void OnDestroy()
        {
            // Both components belong to the same scene object and can be destroyed in either order.
            if (ReferenceEquals(session, null)) return;
            session.Inventory.Changed -= RequestCheckpoint;
            session.Progress.DiscoveryChanged -= RequestCheckpoint;
            session.FacilityLayoutChanged -= RequestCheckpoint;
            session.Cultivation.Watered -= OnWatered;
            session.Cultivation.TimeAdvanced -= MarkDirty;
            session.Exploration.TimeAdvanced -= MarkDirty;
            session.Exploration.JourneyChanged -= RequestCheckpoint;
            if (session.Tutorial != null) session.Tutorial.ProgressChanged -= RequestCheckpoint;
        }
    }
}
