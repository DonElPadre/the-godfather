# Module: Polls

## Group: poll
<details><summary markdown='span'>Expand for additional information</summary><p>

*Starts a new poll in the current channel. You can provide also the time for the poll to run.*

**Overload 2:**

`[time span]` : *Time for poll to run.*

`[string...]` : *Question.*

**Overload 1:**

`[string]` : *Question.*

`[time span]` : *Time for poll to run.*

**Overload 0:**

`[string...]` : *Question.*

**Examples:**

```xml
!poll Do you vote for User1 or User2?
!poll 5m Do you vote for User1 or User2?
```
</p></details>

---

### poll stop
<details><summary markdown='span'>Expand for additional information</summary><p>

*Stops a running poll.*

**Requires user permissions:**
`Administrator`

**Aliases:**
`end, cancel`

</p></details>

---

## Group: vote
<details><summary markdown='span'>Expand for additional information</summary><p>

*Commands for voting in running polls. Group call registers a vote in the current poll for the option you entered.*

**Aliases:**
`votefor, vf`

**Arguments:**

`[int]` : *Option to vote for.*

**Examples:**

```xml
!vote 1
```
</p></details>

---

### vote cancel
<details><summary markdown='span'>Expand for additional information</summary><p>

*Cancel your vote in the current poll.*

**Aliases:**
`c, reset`

</p></details>

---

