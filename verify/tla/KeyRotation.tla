---------------------------- MODULE KeyRotation ----------------------------
EXTENDS Integers

CONSTANTS
    Gap,
    Lifespan,
    TokenLifetime,
    PublishDelay,
    FailureRetryDelay,
    AttemptTimeout,
    WorstCaseRetry,
    MaxTime,
    SignerReadsClockTwice,
    ValidatorCountsRetryOnce

ASSUME ValidConfig ==
    /\ Gap > FailureRetryDelay + 2 * WorstCaseRetry
    /\ WorstCaseRetry >= AttemptTimeout
    /\ AttemptTimeout >= 1
    /\ IF ValidatorCountsRetryOnce
          THEN Lifespan > Gap + FailureRetryDelay + WorstCaseRetry + TokenLifetime + PublishDelay
          ELSE Lifespan > Gap + FailureRetryDelay + 2 * WorstCaseRetry + AttemptTimeout + TokenLifetime + PublishDelay

None == [none |-> TRUE]
Forever == MaxTime + 1

VARIABLES now, keys, lastFinish, phase, wake, roundStart, slotFailed, cache, pending, token

vars == <<now, keys, lastFinish, phase, wake, roundStart, slotFailed, cache, pending, token>>

Max(a, b) == IF a > b THEN a ELSE b
Min(a, b) == IF a < b THEN a ELSE b

NextSlot(t) == (t \div Gap + 1) * Gap

CanSign(k, t) == k.expires > t + TokenLifetime
Published(k, t) == k.created + PublishDelay <= t

Newest(S) == CHOOSE k \in S : \A j \in S : j.created <= k.created
Oldest(S) == CHOOSE k \in S : \A j \in S : k.created <= j.created

NoJob == -1

Running ==
    /\ now = 0
    /\ keys = {[created |-> 0, expires |-> Lifespan]}
    /\ lastFinish = 0
    /\ wake = 0

Bootstrap ==
    /\ now \in 0..(Gap - 1)
    /\ keys = {}
    /\ lastFinish = NoJob
    /\ wake = now

Init ==
    /\ Running \/ Bootstrap
    /\ phase = "waiting"
    /\ roundStart = 0
    /\ slotFailed = FALSE
    /\ cache = None
    /\ pending = None
    /\ token = None

WorkerWakes ==
    /\ phase = "waiting"
    /\ now = wake
    /\ IF lastFinish # NoJob /\ NextSlot(lastFinish) > now
          THEN /\ wake' = NextSlot(lastFinish)
               /\ UNCHANGED <<phase, roundStart>>
          ELSE /\ phase' = "rotating"
               /\ roundStart' = now
               /\ UNCHANGED wake
    /\ UNCHANGED <<now, keys, lastFinish, slotFailed, cache, pending, token>>

RoundFails ==
    /\ phase = "rotating"
    /\ ~slotFailed
    /\ phase' = "waiting"
    /\ wake' = now + FailureRetryDelay
    /\ slotFailed' = TRUE
    /\ UNCHANGED <<now, keys, lastFinish, roundStart, cache, pending, token>>

RoundSucceeds ==
    /\ phase = "rotating"
    /\ \E created \in Max(roundStart, now - AttemptTimeout)..Min(now, roundStart + WorstCaseRetry - AttemptTimeout) :
          keys' = keys \cup {[created |-> created, expires |-> created + Lifespan]}
    /\ lastFinish' = now
    /\ phase' = "waiting"
    /\ wake' = NextSlot(now)
    /\ slotFailed' = FALSE
    /\ UNCHANGED <<now, roundStart, cache, pending, token>>

FirstKeyOlderThanPublishDelay(t) == \E k \in keys : t - k.created > PublishDelay

Reusable(t) == cache # None /\ t < cache.until /\ CanSign(cache.key, t)

PublishedKeys(t) == {k \in keys : Published(k, t)}
IsPublished(t) == PublishedKeys(t) # {} /\ CanSign(Newest(PublishedKeys(t)), t)
Fresh(t) == IF IsPublished(t) THEN Newest(PublishedKeys(t)) ELSE Newest(keys)
Unpublished(t) == {k \in keys : k.created > t - PublishDelay}
ReusableUntil(t) ==
    IF IsPublished(t) \/ Unpublished(t) = {} THEN Forever
    ELSE Oldest(Unpublished(t)).created + PublishDelay

SignStarts ==
    /\ pending = None
    /\ keys # {}
    /\ IF Reusable(now)
          THEN /\ pending' = [key |-> cache.key, at |-> now,
                              publishedCanSign |-> \E k \in keys : Published(k, now) /\ CanSign(k, now),
                              firstKeyOlder |-> FirstKeyOlderThanPublishDelay(now)]
               /\ UNCHANGED cache
          ELSE /\ CanSign(Fresh(now), now)
               /\ cache' = [key |-> Fresh(now), until |-> ReusableUntil(now)]
               /\ pending' = [key |-> Fresh(now), at |-> now,
                              publishedCanSign |-> \E k \in keys : Published(k, now) /\ CanSign(k, now),
                              firstKeyOlder |-> FirstKeyOlderThanPublishDelay(now)]
    /\ UNCHANGED <<now, keys, lastFinish, phase, wake, roundStart, slotFailed, token>>

SignFinishes ==
    /\ pending # None
    /\ token' = [key |-> pending.key, at |-> pending.at, publishedCanSign |-> pending.publishedCanSign,
                  firstKeyOlder |-> pending.firstKeyOlder,
                  expires |-> (IF SignerReadsClockTwice THEN now ELSE pending.at) + TokenLifetime]
    /\ pending' = None
    /\ UNCHANGED <<now, keys, lastFinish, phase, wake, roundStart, slotFailed, cache>>

Tick ==
    /\ now < MaxTime
    /\ phase = "waiting" => now < wake
    /\ phase = "rotating" => now < roundStart + WorstCaseRetry
    /\ now' = now + 1
    /\ UNCHANGED <<keys, lastFinish, phase, wake, roundStart, slotFailed, cache, pending, token>>

Next ==
    \/ WorkerWakes
    \/ RoundFails
    \/ RoundSucceeds
    \/ SignStarts
    \/ SignFinishes
    \/ Tick

Spec == Init /\ [][Next]_vars

K1_TokenExpiresBeforeItsKey == token # None => token.expires < token.key.expires

K2_SignsWithAPublishedKeyWhenOneCanSign ==
    token # None /\ token.publishedCanSign => Published(token.key, token.at)

K3_AlwaysHasAKeyThatCanSign == keys # {} => \E k \in keys : CanSign(k, now)

K16_NeverSignsWithAKeyYoungerThanPublishDelay ==
    token # None /\ token.firstKeyOlder => token.at - token.key.created >= PublishDelay

=============================================================================
