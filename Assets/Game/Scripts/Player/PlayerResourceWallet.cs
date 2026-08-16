using System;
using UnityEngine;

namespace MicroJam.Game
{
    public enum PlayerResourceType
    {
        Wood,
        Stone
    }

    public readonly struct ResourceWalletChangedEvent
    {
        public ResourceWalletChangedEvent(PlayerResourceType resourceType, int previousAmount, int currentAmount)
        {
            ResourceType = resourceType;
            PreviousAmount = previousAmount;
            CurrentAmount = currentAmount;
        }

        public PlayerResourceType ResourceType { get; }
        public int PreviousAmount { get; }
        public int CurrentAmount { get; }
        public int Delta => CurrentAmount - PreviousAmount;
    }

    public sealed class PlayerResourceWallet : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingWood = 20;
        [SerializeField, Min(0)] private int startingStone = 20;
        [SerializeField] private bool initializeOnAwake = true;
        [SerializeField, Min(0)] private int wood = 20;
        [SerializeField, Min(0)] private int stone = 20;

        public int StartingWood => startingWood;
        public int StartingStone => startingStone;
        public int Wood => wood;
        public int Stone => stone;

        public event Action<ResourceWalletChangedEvent> ResourceChanged;

        public void Configure(int configuredStartingWood, int configuredStartingStone)
        {
            startingWood = Mathf.Max(0, configuredStartingWood);
            startingStone = Mathf.Max(0, configuredStartingStone);
            wood = startingWood;
            stone = startingStone;
            initializeOnAwake = true;
        }

        public int GetWood() => wood;
        public int GetStone() => stone;
        public bool AddWood(int amount) => TryAdd(PlayerResourceType.Wood, amount);
        public bool AddStone(int amount) => TryAdd(PlayerResourceType.Stone, amount);
        public bool CanAffordWood(int amount) => CanAfford(PlayerResourceType.Wood, amount);
        public bool CanAffordStone(int amount) => CanAfford(PlayerResourceType.Stone, amount);
        public bool SpendWood(int amount) => TrySpend(PlayerResourceType.Wood, amount);
        public bool SpendStone(int amount) => TrySpend(PlayerResourceType.Stone, amount);

        public bool CanAfford(int woodAmount, int stoneAmount)
        {
            return woodAmount >= 0 && stoneAmount >= 0 && wood >= woodAmount && stone >= stoneAmount;
        }

        public bool TrySpend(int woodAmount, int stoneAmount)
        {
            if (!CanAfford(woodAmount, stoneAmount))
            {
                return false;
            }

            SetAndNotify(PlayerResourceType.Wood, wood - woodAmount);
            SetAndNotify(PlayerResourceType.Stone, stone - stoneAmount);
            return true;
        }

        public bool TryAdd(int woodAmount, int stoneAmount)
        {
            if (woodAmount < 0 || stoneAmount < 0 || (woodAmount == 0 && stoneAmount == 0))
            {
                return false;
            }

            int nextWood = (int)Math.Min(int.MaxValue, (long)wood + woodAmount);
            int nextStone = (int)Math.Min(int.MaxValue, (long)stone + stoneAmount);
            SetAndNotify(PlayerResourceType.Wood, nextWood);
            SetAndNotify(PlayerResourceType.Stone, nextStone);
            return true;
        }

        public int Get(PlayerResourceType resourceType)
        {
            return resourceType == PlayerResourceType.Wood ? wood : stone;
        }

        public bool CanAfford(PlayerResourceType resourceType, int amount)
        {
            return amount >= 0 && Get(resourceType) >= amount;
        }

        public bool TryAdd(PlayerResourceType resourceType, int amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            int previous = Get(resourceType);
            int current = (int)Math.Min(int.MaxValue, (long)previous + amount);
            if (current == previous)
            {
                return false;
            }

            Set(resourceType, current);
            ResourceChanged?.Invoke(new ResourceWalletChangedEvent(resourceType, previous, current));
            return true;
        }

        public bool TrySpend(PlayerResourceType resourceType, int amount)
        {
            if (amount <= 0 || !CanAfford(resourceType, amount))
            {
                return false;
            }

            int previous = Get(resourceType);
            int current = previous - amount;
            Set(resourceType, current);
            ResourceChanged?.Invoke(new ResourceWalletChangedEvent(resourceType, previous, current));
            return true;
        }

        public void ResetForNewRun()
        {
            SetAndNotify(PlayerResourceType.Wood, startingWood);
            SetAndNotify(PlayerResourceType.Stone, startingStone);
        }

        private void Awake()
        {
            if (initializeOnAwake)
            {
                wood = startingWood;
                stone = startingStone;
            }
            else
            {
                wood = Mathf.Max(0, wood);
                stone = Mathf.Max(0, stone);
            }
        }

        private void SetAndNotify(PlayerResourceType resourceType, int amount)
        {
            int previous = Get(resourceType);
            int current = Mathf.Max(0, amount);
            Set(resourceType, current);
            if (previous != current)
            {
                ResourceChanged?.Invoke(new ResourceWalletChangedEvent(resourceType, previous, current));
            }
        }

        private void Set(PlayerResourceType resourceType, int amount)
        {
            if (resourceType == PlayerResourceType.Wood)
            {
                wood = Mathf.Max(0, amount);
            }
            else
            {
                stone = Mathf.Max(0, amount);
            }
        }

        private void OnValidate()
        {
            startingWood = Mathf.Max(0, startingWood);
            startingStone = Mathf.Max(0, startingStone);
            wood = Mathf.Max(0, wood);
            stone = Mathf.Max(0, stone);
        }
    }
}
