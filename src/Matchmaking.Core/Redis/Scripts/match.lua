-- KEYS[1] tickets hash, KEYS[2] region queue, KEYS[3] match id sequence
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
    local t = initial + perSecond * (now - enqueued)
    if t > maxTolerance then
        t = maxTolerance
    end
    return t
end

-- walk the queue ordered by mmr and group neighbours that fit in the window
local queue = redis.call('ZRANGE', KEYS[2], 0, -1, 'WITHSCORES')
local entries = {}
for i = 1, #queue, 2 do
    local id = queue[i]
    local ticket = redis.call('HGET', KEYS[1], id)
    if ticket then
        local enqueued = tonumber(string.match(ticket, '|(%d+)$'))
        entries[#entries + 1] = { id = id, mmr = tonumber(queue[i + 1]), enqueued = enqueued, tol = tolerance(enqueued) }
    else
        redis.call('ZREM', KEYS[2], id)
    end
end

local result = {}
local i = 1
while i + size - 1 <= #entries do
    local first = entries[i]
    local last = entries[i + size - 1]
    local tol = first.tol
    for j = i + 1, i + size - 1 do
        if entries[j].tol < tol then
            tol = entries[j].tol
        end
    end

    if last.mmr - first.mmr <= tol then
        local matchId = redis.call('INCR', KEYS[3])
        local players = {}
        for j = i, i + size - 1 do
            local e = entries[j]
            players[#players + 1] = e.id .. ':' .. e.mmr .. ':' .. e.enqueued
        end
        local payload = matchId .. '|' .. region .. '|' .. now .. '|' .. table.concat(players, ',')
        for j = i, i + size - 1 do
            local id = entries[j].id
            redis.call('ZREM', KEYS[2], id)
            redis.call('HDEL', KEYS[1], id)
            redis.call('SET', assignPrefix .. id, payload, 'PX', ttl)
        end
        result[#result + 1] = payload
        i = i + size
    else
        i = i + 1
    end
end

return result
