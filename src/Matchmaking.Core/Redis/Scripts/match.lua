-- KEYS[1] tickets hash, KEYS[2] region queue by mmr, KEYS[3] region queue by enqueue time,
-- KEYS[4] match id sequence
-- ARGV[1] now (unix ms), ARGV[2] initial tolerance, ARGV[3] tolerance per second,
-- ARGV[4] max tolerance, ARGV[5] players per match, ARGV[6] region,
-- ARGV[7] assignment key prefix, ARGV[8] assignment ttl (ms)
local now = tonumber(ARGV[1])
local initial = tonumber(ARGV[2])
local perSecond = tonumber(ARGV[3])
local maxTolerance = tonumber(ARGV[4])
local size = tonumber(ARGV[5])
local region = ARGV[6]
local assignPrefix = ARGV[7]
local ttl = ARGV[8]

local function tolerance(enqueued)
    local t = initial + perSecond * (now - enqueued) / 1000
    if t > maxTolerance then
        t = maxTolerance
    end
    return t
end

-- Oldest tickets go first and use their own window, which is the widest one.
-- The best candidates are always the nearest by mmr, so we only need to look
-- at size - 1 positions on each side of the anchor.
local result = {}
local waiting = redis.call('ZRANGE', KEYS[3], 0, -1, 'WITHSCORES')

for i = 1, #waiting, 2 do
    local anchor = waiting[i]
    local rank = redis.call('ZRANK', KEYS[2], anchor)

    -- nil when it was already picked by an older ticket in this same tick
    if rank then
        local tol = tolerance(tonumber(waiting[i + 1]))
        local from = rank - (size - 1)
        if from < 0 then
            from = 0
        end

        local around = redis.call('ZRANGE', KEYS[2], from, rank + size - 1, 'WITHSCORES')
        local below = {}
        local above = {}
        local anchorMmr
        for j = 1, #around, 2 do
            local e = { id = around[j], mmr = tonumber(around[j + 1]) }
            if e.id == anchor then
                anchorMmr = e.mmr
            elseif anchorMmr then
                above[#above + 1] = e
            else
                table.insert(below, 1, e)
            end
        end

        local picked = { { id = anchor, mmr = anchorMmr } }
        local b, a = 1, 1
        while #picked < size do
            local lower = below[b]
            local upper = above[a]
            local pick, distance
            if lower and (not upper or anchorMmr - lower.mmr <= upper.mmr - anchorMmr) then
                pick, distance = lower, anchorMmr - lower.mmr
                b = b + 1
            elseif upper then
                pick, distance = upper, upper.mmr - anchorMmr
                a = a + 1
            end

            if not pick or distance > tol then
                break
            end
            picked[#picked + 1] = pick
        end

        if #picked == size then
            local matchId = redis.call('INCR', KEYS[4])
            local players = {}
            for _, p in ipairs(picked) do
                local enqueued = redis.call('ZSCORE', KEYS[3], p.id)
                players[#players + 1] = p.id .. ':' .. p.mmr .. ':' .. enqueued
            end

            local payload = matchId .. '|' .. region .. '|' .. now .. '|' .. table.concat(players, ',')
            for _, p in ipairs(picked) do
                redis.call('ZREM', KEYS[2], p.id)
                redis.call('ZREM', KEYS[3], p.id)
                redis.call('HDEL', KEYS[1], p.id)
                redis.call('SET', assignPrefix .. p.id, payload, 'PX', ttl)
            end
            result[#result + 1] = payload
        end
    end
end

return result
