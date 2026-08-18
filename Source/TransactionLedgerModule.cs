using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonistAwareness
{
    // WHO A TRANSACTION IS BETWEEN. One shape for every party the
    // economy can have, because a schema that only works when one side
    // is the player is the defect the audited mods all share: their
    // settlement paths bind to a player trade session, a player home
    // map, or a colony transactor.
    public enum CAPartyKind
    { Unknown, Pawn, Organization, Settlement, Player }

    // WHY GOODS MOVED. A transfer is not a sale unless someone sold
    // something. Short payment does not silently become credit; the
    // cause has to say what happened, because taxation, requisition,
    // gift and purchase are different acts with different judgements.
    public enum CATransferCause
    {
        Sale, Gift, Subsidy, Levy, Requisition, Procurement,
        Contribution, Custody, CommonTitle
    }

    // WHAT AUTHORIZES DEFERRED PAYMENT. Without terms, a buyer who
    // cannot pay does not buy. Terms are agreed before the transfer,
    // by contract, custom, or an authority entitled to bind the
    // parties - never inferred from an empty purse.
    public sealed class CACreditTerms : IExposable
    {
        public bool allowed;
        public int dueTick = -1;        // -1 = on demand
        public string basis;            // contract, custom, authority
        public CAParty guarantor;       // optional third party

        public void ExposeData()
        {
            Scribe_Values.Look(ref allowed, "allowed", false);
            Scribe_Values.Look(ref dueTick, "dueTick", -1);
            Scribe_Values.Look(ref basis, "basis");
            Scribe_Deep.Look(ref guarantor, "guarantor");
        }
    }

    // WHERE AN OBLIGATION STANDS. Debt is a continuing state, not an
    // act. Default is the act, and only once payment is due and
    // unsatisfied.
    public enum CAObligationState
    { Outstanding, Due, Satisfied, Defaulted, Renegotiated, Forgiven }

    public sealed class CAParty : IExposable
    {
        public CAPartyKind kind = CAPartyKind.Unknown;
        public string orgKey;        // organization or settlement key
        public int pawnId = -1;      // thingIDNumber
        public string label;

        public static CAParty Of(Pawn p)
        {
            if (p == null) return new CAParty();
            return new CAParty
            {
                kind = p.Faction != null && p.Faction.IsPlayer
                    ? CAPartyKind.Player : CAPartyKind.Pawn,
                pawnId = p.thingIDNumber,
                label = p.LabelShortCap
            };
        }

        public static CAParty Of(CAOrganization org)
        {
            if (org == null) return new CAParty();
            return new CAParty
            {
                kind = CAPartyKind.Organization,
                orgKey = org.organizationKey,
                label = org.name
            };
        }

        public static CAParty OfSettlement(string key, string name)
        {
            return new CAParty
            {
                kind = CAPartyKind.Settlement,
                orgKey = key,
                label = name
            };
        }

        public bool Matches(CAParty other)
        {
            if (other == null) return false;
            if (pawnId >= 0 || other.pawnId >= 0)
                return pawnId == other.pawnId;
            return !orgKey.NullOrEmpty() && orgKey == other.orgKey;
        }

        public string Display
        {
            get
            {
                return label.NullOrEmpty()
                    ? (orgKey.NullOrEmpty() ? "unknown" : orgKey) : label;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind", CAPartyKind.Unknown);
            Scribe_Values.Look(ref orgKey, "orgKey");
            Scribe_Values.Look(ref pawnId, "pawnId", -1);
            Scribe_Values.Look(ref label, "label");
        }
    }

    // ONE TRANSACTION, WHOLE. Goods or a service moved, at a valuation,
    // paid in some medium, with whatever was not paid surviving as a
    // named obligation between named parties.
    public sealed class CATransaction : IExposable
    {
        public int id;
        public CAParty buyer = new CAParty();
        public CAParty seller = new CAParty();
        // premises: who owns it, who runs it. Frequently not the seller.
        public CAParty owner = new CAParty();
        public CAParty runBy = new CAParty();

        public string goodDefName;   // null for a service
        public string serviceKind;   // null for goods
        public int quantity;
        public int valuation;

        public CATransferCause cause = CATransferCause.Sale;
        public CACreditTerms terms;
        public bool authorityClaimed;
        public bool consentGiven;
        public string paymentMedium = "none";
        public int paidAmount;
        public int unpaidBalance;

        public CAParty debtor = new CAParty();
        public CAParty creditor = new CAParty();

        public int tick;
        public string provenance;
        public string settlementStatus = "unsettled";

        public string What
        {
            get
            {
                if (!serviceKind.NullOrEmpty()) return serviceKind;
                if (goodDefName.NullOrEmpty()) return "nothing";
                return quantity + " x " + goodDefName;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", 0);
            Scribe_Deep.Look(ref buyer, "buyer");
            Scribe_Deep.Look(ref seller, "seller");
            Scribe_Deep.Look(ref owner, "owner");
            Scribe_Deep.Look(ref runBy, "runBy");
            Scribe_Values.Look(ref goodDefName, "goodDefName");
            Scribe_Values.Look(ref serviceKind, "serviceKind");
            Scribe_Values.Look(ref quantity, "quantity", 0);
            Scribe_Values.Look(ref valuation, "valuation", 0);
            Scribe_Values.Look(ref cause, "cause", CATransferCause.Sale);
            Scribe_Deep.Look(ref terms, "terms");
            Scribe_Values.Look(ref authorityClaimed, "authorityClaimed",
                false);
            Scribe_Values.Look(ref consentGiven, "consentGiven", false);
            Scribe_Values.Look(ref paymentMedium, "paymentMedium", "none");
            Scribe_Values.Look(ref paidAmount, "paidAmount", 0);
            Scribe_Values.Look(ref unpaidBalance, "unpaidBalance", 0);
            Scribe_Deep.Look(ref debtor, "debtor");
            Scribe_Deep.Look(ref creditor, "creditor");
            Scribe_Values.Look(ref tick, "tick", 0);
            Scribe_Values.Look(ref provenance, "provenance");
            Scribe_Values.Look(ref settlementStatus, "settlementStatus",
                "unsettled");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                buyer = buyer ?? new CAParty();
                seller = seller ?? new CAParty();
                owner = owner ?? new CAParty();
                runBy = runBy ?? new CAParty();
                debtor = debtor ?? new CAParty();
                creditor = creditor ?? new CAParty();
            }
        }
    }

    // A SHORTFALL THAT SURVIVED. Gastronomy's useful idea: an
    // incomplete payment is an obligation, not a discarded local. Its
    // implementation ties debt to a restaurant patron on one map; this
    // ties it to any two parties and persists with the world.
    public sealed class CADebt : IExposable
    {
        public CAParty debtor = new CAParty();
        public CAParty creditor = new CAParty();
        public int amount;
        public int sinceTick;
        public int dueTick = -1;     // -1 = on demand
        public string origin;
        public string basis;         // what created the obligation
        public CAObligationState state = CAObligationState.Outstanding;
        public int defaultedTick = -1;

        public bool IsDue(int now)
        {
            return dueTick >= 0 && now >= dueTick
                && state == CAObligationState.Outstanding;
        }

        public void ExposeData()
        {
            Scribe_Deep.Look(ref debtor, "debtor");
            Scribe_Deep.Look(ref creditor, "creditor");
            Scribe_Values.Look(ref amount, "amount", 0);
            Scribe_Values.Look(ref sinceTick, "sinceTick", 0);
            Scribe_Values.Look(ref dueTick, "dueTick", -1);
            Scribe_Values.Look(ref origin, "origin");
            Scribe_Values.Look(ref basis, "basis");
            Scribe_Values.Look(ref state, "state",
                CAObligationState.Outstanding);
            Scribe_Values.Look(ref defaultedTick, "defaultedTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                debtor = debtor ?? new CAParty();
                creditor = creditor ?? new CAParty();
            }
        }
    }

    // THE ORGANIZATION LEDGER, and the economy's source of truth for
    // title, payment and obligation.
    //
    // A WorldComponent, not a MapComponent. Every audited mod scoped
    // its economy to a map - Storefront's StoresManager, Gastronomy's
    // RestaurantsManager, Hospitality's components - which is why none
    // of them can settle between two settlements while neither map is
    // loaded. That is the whole gap CA has to fill.
    //
    // ON-MAP, real Things remain authoritative: goods and currency move
    // as objects and the ledger records what happened. OFF-MAP, the
    // same call adjusts material aggregates and balances instead. Both
    // paths write the same transaction, so a settlement that trades
    // while unloaded materialises holding what the ledger says it
    // holds rather than rerolling.
    public sealed class CATransactionLedger : WorldComponent
    {
        private List<CATransaction> transactions =
            new List<CATransaction>();
        private List<CADebt> debts = new List<CADebt>();
        private int nextId = 1;

        public CATransactionLedger(World world) : base(world) { }

        public IReadOnlyList<CATransaction> Transactions
        {
            get { return transactions; }
        }

        public IReadOnlyList<CADebt> Debts { get { return debts; } }

        public static CATransactionLedger Current
        {
            get
            {
                try
                {
                    return Find.World?
                        .GetComponent<CATransactionLedger>();
                }
                catch { return null; }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref transactions, "transactions",
                LookMode.Deep);
            Scribe_Collections.Look(ref debts, "debts", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                transactions = transactions ?? new List<CATransaction>();
                debts = debts ?? new List<CADebt>();
            }
        }

        // ---- debt, adapted from Gastronomy's pattern ---------------

        public CADebt DebtBetween(CAParty debtor, CAParty creditor)
        {
            for (int i = 0; i < debts.Count; i++)
                if (debts[i].debtor.Matches(debtor)
                    && debts[i].creditor.Matches(creditor))
                    return debts[i];
            return null;
        }

        // AN OBLIGATION IS CREATED BY A BASIS, never by an empty purse.
        // `basis` names what binds the debtor - agreed credit terms, a
        // agreement, levy, or leader's order. A caller without one
        // has not created a debt; it has failed a transaction.
        public void AddDebt(CAParty debtor, CAParty creditor,
            int amount, string origin, string basis, int dueTick)
        {
            if (amount <= 0 || debtor == null || creditor == null) return;
            if (basis.NullOrEmpty()) return;
            CADebt existing = DebtBetween(debtor, creditor);
            if (existing != null)
            {
                existing.amount += amount;
                return;
            }
            debts.Add(new CADebt
            {
                debtor = debtor,
                creditor = creditor,
                amount = amount,
                sinceTick = Find.TickManager?.TicksGame ?? 0,
                origin = origin,
                // the basis was validated above but silently dropped
                // before the first live caller existed - an obligation
                // must carry what binds the debtor, and its due date
                basis = basis,
                dueTick = dueTick
            });
        }

        public int PayDebt(CAParty debtor, CAParty creditor, int amount)
        {
            CADebt debt = DebtBetween(debtor, creditor);
            if (debt == null || amount <= 0) return 0;
            int paid = Mathf.Min(amount, debt.amount);
            debt.amount -= paid;
            if (debt.amount <= 0) debts.Remove(debt);
            return paid;
        }

        public int OwedBy(CAParty debtor)
        {
            int total = 0;
            for (int i = 0; i < debts.Count; i++)
                if (debts[i].debtor.Matches(debtor))
                    total += debts[i].amount;
            return total;
        }

        // ---- execution --------------------------------------------

        // ON-MAP SALE. The audited Storefront path, reimplemented so
        // the ledger owns the outcome: value the goods, move what
        // currency the buyer actually has into the seller's holder,
        // move the goods, and turn any shortfall into a real debt
        // instead of a discarded local.
        // A SALE ON SHORT PAYMENT REQUIRES EXPLICIT CREDIT TERMS.
        // Without them the sale FAILS and nothing moves. Insufficient
        // silver is a buyer who cannot buy, not an obligation. Moving
        // the goods anyway would make every seller a lender by default
        // - universal seller-financed credit, which is a political
        // rule nobody chose.
        public CATransaction ExecuteSale(Pawn buyerPawn,
            ThingOwner sellerHolder, Thing good, int count,
            CAParty seller, CAParty owner, CAParty runBy,
            CACreditTerms terms, string provenance)
        {
            var t = NewTransaction(CAParty.Of(buyerPawn), seller,
                owner, runBy, provenance);
            t.cause = CATransferCause.Sale;
            t.terms = terms;
            t.consentGiven = true;
            if (good == null || count <= 0 || buyerPawn == null)
                return Record(t, "void");

            t.goodDefName = good.def.defName;
            t.quantity = count;
            t.valuation = Mathf.CeilToInt(good.MarketValue * count);
            t.paymentMedium = ThingDefOf.Silver.defName;

            ThingOwner buyerInv = buyerPawn.inventory?.innerContainer;
            Thing silver = buyerInv?.FirstOrDefault(
                i => i.def == ThingDefOf.Silver);
            int available = silver?.stackCount ?? 0;

            bool credited = terms != null && terms.allowed
                && !terms.basis.NullOrEmpty();
            if (available < t.valuation && !credited)
            {
                t.paidAmount = 0;
                t.unpaidBalance = t.valuation;
                return Record(t, "refused-insufficient-funds");
            }

            int paid = 0;
            if (silver != null && t.valuation > 0)
            {
                int give = Mathf.Min(available, t.valuation);
                if (give > 0)
                    paid = sellerHolder != null
                        ? buyerInv.TryTransferToContainer(silver,
                            sellerHolder, give)
                        : DropPayment(buyerInv, silver, buyerPawn, give);
            }
            t.paidAmount = paid;
            t.unpaidBalance = Mathf.Max(0, t.valuation - paid);

            if (buyerInv != null) buyerInv.TryAdd(good.SplitOff(count));

            if (t.unpaidBalance > 0)
            {
                t.debtor = t.buyer;
                t.creditor = t.seller;
                AddDebt(t.debtor, t.creditor, t.unpaidBalance,
                    "sale #" + t.id, terms.basis, terms.dueTick);
            }
            return Record(t, t.unpaidBalance > 0
                ? (paid > 0 ? "part-paid-on-terms" : "on-credit")
                : "settled");
        }

        // MATERIAL AND TITLE MOVING FOR SOME OTHER REASON - a gift, a
        // levy, a requisition, a contribution to common stores, goods
        // held in custody. No payment is implied, and no obligation
        // arises unless compensation was actually promised and the
        // caller names what promised it.
        public CATransaction ExecuteTransfer(CAParty from, CAParty to,
            CATransferCause cause, string what, int quantity,
            int valuation, bool authorityClaimed, bool consentGiven,
            int compensationOwed, string compensationBasis,
            int dueTick, string provenance)
        {
            var t = NewTransaction(from, to, to, to, provenance);
            t.cause = cause;
            t.serviceKind = what;
            t.quantity = quantity;
            t.valuation = Mathf.Max(0, valuation);
            t.authorityClaimed = authorityClaimed;
            t.consentGiven = consentGiven;
            t.unpaidBalance = Mathf.Max(0, compensationOwed);
            if (t.unpaidBalance > 0 && !compensationBasis.NullOrEmpty())
            {
                t.debtor = to;
                t.creditor = from;
                AddDebt(to, from, t.unpaidBalance,
                    cause + " #" + t.id, compensationBasis, dueTick);
            }
            return Record(t, t.unpaidBalance > 0
                ? "compensation-owed" : "transferred");
        }

        // Only a due, unpaid debt creates a political-belief event.
        // Ordinary credit does not count as nonpayment.
        public List<CADebt> MarkDue(int now)
        {
            var newlyDue = new List<CADebt>();
            for (int i = 0; i < debts.Count; i++)
                if (debts[i].IsDue(now))
                {
                    debts[i].state = CAObligationState.Due;
                    newlyDue.Add(debts[i]);
                }
            return newlyDue;
        }

        public bool Default(CADebt debt, int now)
        {
            if (debt == null || debt.state != CAObligationState.Due)
                return false;
            debt.state = CAObligationState.Defaulted;
            debt.defaultedTick = now;
            return true;
        }

        public bool Renegotiate(CADebt debt, int newDueTick,
            string basis)
        {
            if (debt == null || basis.NullOrEmpty()) return false;
            debt.dueTick = newDueTick;
            debt.basis = basis;
            debt.state = CAObligationState.Renegotiated;
            return true;
        }

        private static int DropPayment(ThingOwner inv, Thing silver,
            Pawn actor, int amount)
        {
            Thing dropped;
            return inv.TryDrop(silver, actor.Position, actor.Map,
                ThingPlaceMode.Near, amount, out dropped) ? amount : 0;
        }

        // OFF-MAP SETTLEMENT. No Things exist to move, so the same
        // transaction is written against material aggregates and
        // balances. The ledger entry is identical in shape, which is
        // what lets a materialising settlement reproduce its state
        // rather than reroll it.
        public CATransaction ExecuteAbstract(CAParty buyer,
            CAParty seller, string what, int quantity, int valuation,
            int available, string medium, CACreditTerms terms,
            string provenance)
        {
            var t = NewTransaction(buyer, seller, seller, seller,
                provenance);
            t.terms = terms;
            t.serviceKind = what;
            t.quantity = quantity;
            t.valuation = Mathf.Max(0, valuation);
            t.paymentMedium = medium.NullOrEmpty() ? "none" : medium;
            t.paidAmount = Mathf.Clamp(available, 0, t.valuation);
            t.unpaidBalance = t.valuation - t.paidAmount;
            if (t.unpaidBalance > 0)
            {
                // same rule off-map as on: no terms, no credit
                bool credited = terms != null && terms.allowed
                    && !terms.basis.NullOrEmpty();
                if (!credited)
                {
                    t.paidAmount = 0;
                    return Record(t, "refused-insufficient-funds");
                }
                t.debtor = buyer;
                t.creditor = seller;
                AddDebt(buyer, seller, t.unpaidBalance,
                    "obligation #" + t.id, terms.basis, terms.dueTick);
            }
            return Record(t, t.unpaidBalance > 0
                ? (t.paidAmount > 0 ? "part-paid-on-terms" : "on-credit")
                : "settled");
        }

        private CATransaction NewTransaction(CAParty buyer,
            CAParty seller, CAParty owner, CAParty runBy,
            string provenance)
        {
            return new CATransaction
            {
                id = nextId++,
                buyer = buyer ?? new CAParty(),
                seller = seller ?? new CAParty(),
                owner = owner ?? new CAParty(),
                runBy = runBy ?? new CAParty(),
                tick = Find.TickManager?.TicksGame ?? 0,
                provenance = provenance
            };
        }

        private CATransaction Record(CATransaction t, string status)
        {
            t.settlementStatus = status;
            transactions.Add(t);
            return t;
        }
    }
}
