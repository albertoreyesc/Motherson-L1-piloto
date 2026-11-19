import {describe,it,expect} from 'vitest';
import {annualScope,dailyTrend,validAnnual} from './annualMetrics';
const report={total:{consumed:90,entries:80},months:[{label:'2026-01',consumed:30,entries:20},{label:'2026-02',consumed:60,entries:60}],materialMetrics:[{label:'a',consumed:40,entries:30}],materialMonths:[{label:'2026-01|a',consumed:10,entries:0}],dailyBalances:[{date:'2026-01-29',key:'a',stock:90},{date:'2026-01-30',key:'a',stock:90},{date:'2026-01-30',key:'b',stock:100},{date:'2026-02-02',key:'a',stock:70},{date:'2026-02-02',key:'b',stock:120}]};
describe('annual report scopes',()=>{
 it('uses period-end stocks rather than summing daily stock',()=>{expect(annualScope(report,'','').finalStock).toBe(190);expect(annualScope(report,'2026-01','').initialStock).toBe(200);expect(annualScope(report,'2026-01','a')).toMatchObject({initialStock:100,finalStock:90,consumed:10,entries:0})});
 it('aggregates each day once and matches material/month filters',()=>{expect(dailyTrend(report,'2026-01','')).toEqual([{date:'2026-01-29',stock:90},{date:'2026-01-30',stock:190}]);expect(dailyTrend(report,'2026-02','a')).toEqual([{date:'2026-02-02',stock:70}])});
 it('rejects unrelated or real-data imports',()=>{expect(validAnnual({})).toBe(false);expect(validAnnual({nature:'REAL',engine:'annual-lab-v1'})).toBe(false)});
});
